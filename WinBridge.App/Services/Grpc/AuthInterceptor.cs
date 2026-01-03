using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using WinBridge.App.Services;

namespace WinBridge.App.Services.Grpc;

public class AuthInterceptor(DataService dataService) : Interceptor
{
    private readonly DataService _dataService = dataService;
    private const string SessionTokenHeader = "x-session-token";

    private static readonly Dictionary<string, string> _methodPermissions = new()
    {
        
        { "/winbridge.WinBridgeHost/GetServers", "Server.Read" },
        { "/winbridge.WinBridgeHost/StreamServerMetrics", "Server.Read" }, 
        
    };

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await CheckAccess(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await CheckAccess(context);
        await continuation(request, responseStream, context);
    }

    private async Task CheckAccess(ServerCallContext context)
    {
        var methodName = context.Method;

        if (!_methodPermissions.TryGetValue(methodName, out var requiredPermission))
        {

            return;
        }

        var header = context.RequestHeaders.GetValue(SessionTokenHeader);
        if (string.IsNullOrEmpty(header))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing session token"));
        }

        var module = await _dataService.GetModuleByTokenAsync(header) ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid session token"));
        if (!module.IsAuthorized)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Module not authorized by user"));
        }

        bool hasPermission = false;
        if (!string.IsNullOrEmpty(module.PermissionsRaw))
        {
            try
            {
                var perms = System.Text.Json.JsonSerializer.Deserialize<List<string>>(module.PermissionsRaw);
                if (perms != null && perms.Contains(requiredPermission))
                {
                    hasPermission = true;
                }
            }
            catch {  }
        }

        if (!hasPermission)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, $"Missing required permission: {requiredPermission}"));
        }
    }
}
