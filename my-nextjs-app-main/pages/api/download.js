// file: pages/api/download.js

import path from 'path';
import fs from 'fs';

export default function handler(req, res) {
  // 1. Lấy tên file từ query parameter
  const { file: requestedFile } = req.query;

  // 2. Kiểm tra xem tên file có được cung cấp không
  if (!requestedFile) {
    return res.status(400).json({ error: 'File name is required.' });
  }

  const safeFileName = path.basename(requestedFile);

  // 4. Tạo đường dẫn an toàn đến file
  const privateDirPath = path.join(process.cwd(), 'private_files');
  const filePath = path.join(privateDirPath, safeFileName);

  if (!filePath.startsWith(privateDirPath)) {
      return res.status(403).json({ error: 'Access forbidden.' });
  }
  
  try {
    // Kiểm tra xem file có tồn tại không
    if (!fs.existsSync(filePath)) {
      return res.status(404).json({ error: 'File not found.' });
    }

    // Đọc nội dung file
    const fileBuffer = fs.readFileSync(filePath);

    // Thiết lập các HTTP Header để ép buộc tải xuống
    res.setHeader('Content-Type', 'application/octet-stream');
    res.setHeader(
      'Content-Disposition',
      `attachment; filename="${safeFileName}"`
    );

    // Gửi nội dung file về cho client
    res.send(fileBuffer);

  } catch (error) {
    console.error(error);
    res.status(500).json({ error: 'Internal server error.' });
  }
}