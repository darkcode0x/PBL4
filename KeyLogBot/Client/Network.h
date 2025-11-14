#pragma once
#define WIN32_LEAN_AND_MEAN

#include <windows.h>
#include <windns.h>
#include <string>



constexpr DWORD DNS_OPTIONS = DNS_QUERY_BYPASS_CACHE | DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE;


inline std::string TARGET_DOMAIN = "example.com";


inline const char* DNS_SERVER_IP = "127.0.0.1";  

inline std::string g_outChunk;
inline std::mutex g_outChunkMutex;

int startConnection(const char* domain);
int sendData(int& id, int& packetNumber, const char* domain, const char* data);
int sendDataTypeC(int& id, int& packetNumber, size_t& offset, const char* domain, const char* data);
int sendDataTypeP(int& id, int& packetNumber, size_t& offset, const char* domain, const char* data);
std::string convertToHex(const char* string);
