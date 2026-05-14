#pragma once
#define EXPORT __declspec(dllexport)
extern "C" {
EXPORT char* MCPConnectStdio(const char* serverId, const char* commandLine, const char* workingDirectory, int timeoutMs);
EXPORT char* MCPConnectHttp(const char* serverId, const char* url, const char* customHeader, int timeoutMs);
EXPORT char* MCPDisconnect(const char* serverId);
EXPORT void MCPDisconnectAll();
EXPORT char* MCPRefreshTools(const char* serverId);
EXPORT char* MCPBuildToolsJson();
EXPORT void MCPRegisterToolsForSlot(const char* slotName);
EXPORT bool MCPHasTool(const char* exposedName);
EXPORT char* MCPExecuteTool(const char* exposedName, const char* argumentsJson);
EXPORT char* MCPListServers();
}
