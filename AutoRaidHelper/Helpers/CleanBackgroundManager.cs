using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.D3D11_BIND_FLAG;
using static TerraFX.Interop.DirectX.D3D11_USAGE;
using static TerraFX.Interop.DirectX.DXGI_FORMAT;
using static TerraFX.Interop.Windows.Windows;

namespace AutoRaidHelper.Helpers;

public sealed unsafe class CleanBackgroundManager(IPluginLog log) : IDisposable
{
    private ID3D11Device* _device;
    private ID3D11DeviceContext* _context;

    // 资源
    private ID3D11Texture2D* _capturedTexture;
    private ID3D11Texture2D* _outputTexture;
    private ID3D11Texture2D* _blurTempTexture;
    private ID3D11ShaderResourceView* _capturedSrv;
    private ID3D11ShaderResourceView* _outputSrv;
    private ID3D11ShaderResourceView* _blurTempSrv;
    private ID3D11UnorderedAccessView* _outputUav;
    private ID3D11UnorderedAccessView* _blurTempUav;

    // Shaders
    private ID3D11ComputeShader* _alphaFixShader;
    private ID3D11ComputeShader* _blurHorizontalShader;
    private ID3D11ComputeShader* _blurVerticalShader;

    private int _blurIterations = 3;
    private bool _resourcesReady;
    private int _lastFrameCount = -1;
    private bool _hasAttemptedInit;
    private bool _isDeviceAvailable;

    public void Initialize()
    {
        if (_hasAttemptedInit)
            return;
        _hasAttemptedInit = true;
        UpdateDevice();
    }

    public void DrawBackground(float opacity = 0.8f)
    {
        if (!UpdateDevice())
        {
            log.Warning("DirectX设备未就绪");
            return;
        }
        if (_alphaFixShader == null)
        {
            log.Warning("着色器未加载，无法绘制磨砂背景");
            return;
        }

        try
        {
            var currentFrame = ImGui.GetFrameCount();
            if (_lastFrameCount != currentFrame)
            {
                CaptureAndBlur();
                _lastFrameCount = currentFrame;
            }

            if (!_resourcesReady || _outputSrv == null)
            {
                log.Warning("资源未就绪或输出SRV为空");
                return;
            }

            var drawList = ImGui.GetWindowDrawList();
            var windowPos = ImGui.GetWindowPos();
            var windowSize = ImGui.GetWindowSize();
            var displaySize = ImGui.GetIO().DisplaySize;

            var uv0 = new Vector2(windowPos.X / displaySize.X, windowPos.Y / displaySize.Y);
            var uv1 = new Vector2(
                (windowPos.X + windowSize.X) / displaySize.X,
                (windowPos.Y + windowSize.Y) / displaySize.Y
            );

            drawList.AddImage(
                new ImTextureID(_outputSrv),
                windowPos,
                windowPos + windowSize,
                uv0,
                uv1,
                ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1))
            );
            var overlayColor = ImGui.ColorConvertFloat4ToU32(
                new Vector4(0.1f, 0.1f, 0.1f, opacity)
            );
            drawList.AddRectFilled(windowPos, windowPos + windowSize, overlayColor);
        }
        catch (Exception ex)
        {
            log.Error($"严重错误: {ex.Message}");
        }
    }

    private bool UpdateDevice()
    {
        if (_device != null && _context != null)
            return true;

        try
        {
            var deviceInstance = Device.Instance();
            if (deviceInstance == null)
            {
                log.Debug("Device.Instance() 返回 null");
                return false;
            }

            var context = deviceInstance->D3D11DeviceContext;
            if (context == null)
            {
                log.Debug("D3D11DeviceContext 为 null");
                return false;
            }

            _context = (ID3D11DeviceContext*)context;
            ID3D11Device* dev;
            _context->GetDevice(&dev);
            _device = dev;

            LoadShaders();

            _isDeviceAvailable = true;
            log.Information("DirectX11设备成功获取");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "DirectX11设备获取失败");
            return false;
        }
    }

    private void LoadShaders()
    {
        log.Information("开始加载着色器...");
        _alphaFixShader = LoadShaderFromResource(@"AutoRaidHelper.Shaders.AlphaFix.cso");
        _blurHorizontalShader = LoadShaderFromResource(@"AutoRaidHelper.Shaders.HBlur.cso");
        _blurVerticalShader = LoadShaderFromResource(@"AutoRaidHelper.Shaders.VBlur.cso");

        if (_alphaFixShader != null)
            log.Information("AlphaFix 着色器加载成功");
        if (_blurHorizontalShader != null)
            log.Information("HBlur 着色器加载成功");
        if (_blurVerticalShader != null)
            log.Information("VBlur 着色器加载成功");
    }

    private ID3D11ComputeShader* LoadShaderFromResource(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                log.Error(
                    $"找不到对应路径的嵌入式的资源文件: {resourceName}"
                );
                return null;
            }

            var bytecode = new byte[stream.Length];
            stream.ReadExactly(bytecode);

            ID3D11ComputeShader* cs;
            fixed (byte* pCode = bytecode)
            {
                var hr = _device->CreateComputeShader(pCode, (nuint)bytecode.Length, null, &cs);
                if (FAILED(hr))
                {
                    log.Error(
                        $"无法从资源创建计算着色器: {resourceName}"
                    );
                    return null;
                }
            }
            return cs;
        }
        catch (Exception ex)
        {
            log.Error($"着色器加载失败: {ex.Message}");
            return null;
        }
    }

    private void CaptureAndBlur()
    {
        if (_context == null || _device == null)
            return;
        var deviceInstance = Device.Instance();
        if (deviceInstance == null || deviceInstance->SwapChain == null)
            return;
        var swapChain = (IDXGISwapChain*)deviceInstance->SwapChain->DXGISwapChain;
        if (swapChain == null)
            return;

        ID3D11Texture2D* backBuffer = null;
        Guid texUuid = __uuidof<ID3D11Texture2D>();
        var hr = swapChain->GetBuffer(0, &texUuid, (void**)&backBuffer);
        if (FAILED(hr) || backBuffer == null)
            return;

        D3D11_TEXTURE2D_DESC desc;
        backBuffer->GetDesc(&desc);
        var currentDesc = GetDescSafe(_capturedTexture);
        if (
            _capturedTexture == null
            || currentDesc.Width != desc.Width
            || currentDesc.Height != desc.Height
        )
        {
            if (!ResizeResources(desc))
            {
                backBuffer->Release();
                return;
            }
        }

        if (_capturedTexture != null)
        {
            _context->CopyResource(
                (ID3D11Resource*)_capturedTexture,
                (ID3D11Resource*)backBuffer
            );
            if (_alphaFixShader != null)
                RunComputeShader(
                    _alphaFixShader,
                    _capturedSrv,
                    _outputUav,
                    desc.Width,
                    desc.Height
                );
            if (_blurHorizontalShader != null && _blurVerticalShader != null)
            {
                for (var i = 0; i < _blurIterations; i++)
                {
                    RunComputeShader(
                        _blurHorizontalShader,
                        _outputSrv,
                        _blurTempUav,
                        desc.Width,
                        desc.Height
                    );
                    RunComputeShader(
                        _blurVerticalShader,
                        _blurTempSrv,
                        _outputUav,
                        desc.Width,
                        desc.Height
                    );
                }
            }
        }
        backBuffer->Release();
    }

    private void RunComputeShader(
        ID3D11ComputeShader* shader,
        ID3D11ShaderResourceView* input,
        ID3D11UnorderedAccessView* output,
        uint width,
        uint height
    )
    {
        if (shader == null || _context == null)
            return;
        _context->CSSetShader(shader, null, 0);
        var srvs = stackalloc ID3D11ShaderResourceView*[1] { input };
        _context->CSSetShaderResources(0, 1, srvs);
        var uavs = stackalloc ID3D11UnorderedAccessView*[1] { output };
        _context->CSSetUnorderedAccessViews(0, 1, uavs, null);
        var dispatchX = (width + 7) / 8;
        var dispatchY = (height + 7) / 8;
        _context->Dispatch(dispatchX, dispatchY, 1);
        var nullSrvs = stackalloc ID3D11ShaderResourceView*[1] { null };
        _context->CSSetShaderResources(0, 1, nullSrvs);
        var nullUavs =
            stackalloc ID3D11UnorderedAccessView*[1] { null };
        _context->CSSetUnorderedAccessViews(0, 1, nullUavs, null);
    }

    private bool ResizeResources(D3D11_TEXTURE2D_DESC bbDesc)
    {
        DisposeTextures();
        var format = bbDesc.Format;
        if (format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB)
            format = DXGI_FORMAT_R8G8B8A8_UNORM;
        if (format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB)
            format = DXGI_FORMAT_B8G8R8A8_UNORM;

        var texDesc = new D3D11_TEXTURE2D_DESC
        {
            Width = bbDesc.Width,
            Height = bbDesc.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
            Usage = D3D11_USAGE_DEFAULT,
            BindFlags = (uint)(D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_UNORDERED_ACCESS),
            CPUAccessFlags = 0,
            MiscFlags = 0
        };
        var inputDesc = texDesc;
        inputDesc.BindFlags = (uint)D3D11_BIND_SHADER_RESOURCE;

        ID3D11Texture2D* captured;
        if (FAILED(_device->CreateTexture2D(&inputDesc, null, &captured)))
            return false;
        _capturedTexture = captured;
        ID3D11ShaderResourceView* capturedSrv;
        _device->CreateShaderResourceView((ID3D11Resource*)captured, null, &capturedSrv);
        _capturedSrv = capturedSrv;

        ID3D11Texture2D* output;
        if (FAILED(_device->CreateTexture2D(&texDesc, null, &output)))
            return false;
        _outputTexture = output;
        ID3D11UnorderedAccessView* outputUav;
        _device->CreateUnorderedAccessView((ID3D11Resource*)output, null, &outputUav);
        _outputUav = outputUav;
        ID3D11ShaderResourceView* outputSrv;
        _device->CreateShaderResourceView((ID3D11Resource*)output, null, &outputSrv);
        _outputSrv = outputSrv;

        ID3D11Texture2D* temp;
        if (FAILED(_device->CreateTexture2D(&texDesc, null, &temp)))
            return false;
        _blurTempTexture = temp;
        ID3D11UnorderedAccessView* tempUav;
        _device->CreateUnorderedAccessView((ID3D11Resource*)temp, null, &tempUav);
        _blurTempUav = tempUav;
        ID3D11ShaderResourceView* tempSrv;
        _device->CreateShaderResourceView((ID3D11Resource*)temp, null, &tempSrv);
        _blurTempSrv = tempSrv;

        _resourcesReady = true;
        return true;
    }

    private D3D11_TEXTURE2D_DESC GetDescSafe(ID3D11Texture2D* tex)
    {
        D3D11_TEXTURE2D_DESC desc = default;
        if (tex != null)
            tex->GetDesc(&desc);
        return desc;
    }

    private void DisposeTextures()
    {
        _resourcesReady = false;
        Release(ref _capturedSrv);
        Release(ref _capturedTexture);
        Release(ref _outputSrv);
        Release(ref _outputUav);
        Release(ref _outputTexture);
        Release(ref _blurTempSrv);
        Release(ref _blurTempUav);
        Release(ref _blurTempTexture);
    }

    private void Release<T>(ref T* ptr)
        where T : unmanaged
    {
        if (ptr != null)
        {
            ((IUnknown*)ptr)->Release();
            ptr = null;
        }
    }

    public void Dispose()
    {
        DisposeTextures();
        Release(ref _alphaFixShader);
        Release(ref _blurHorizontalShader);
        Release(ref _blurVerticalShader);
        Release(ref _device);
        _context = null;
    }
}