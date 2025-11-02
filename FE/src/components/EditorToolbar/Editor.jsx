import React from 'react';
import { CKEditor, useCKEditorCloud } from '@ckeditor/ckeditor5-react';
const editorUrl = 'https://cdn.ckeditor.com/ckeditor5/41.3.1/classic/ckeditor.js';

function MyEditor() {
  return (
    <div style={{ margin: '40px' }}>
      <h2>Custom CKEditor Toolbar</h2>

      <CKEditor
        editor={window.ClassicEditor}
        config={{
          toolbar: [
            'undo', 'redo', '|',
            'heading', '|',
            'bold', 'italic', 'underline', '|',
            'link', 'blockQuote', '|',
            'numberedList', 'bulletedList', '|',
            'insertTable', 'mediaEmbed', '|',
            'removeFormat'
          ],
        }}
        data="<p>Bắt đầu viết nội dung ở đây...</p>"
        onReady={(editor) => {
          console.log('✅ CKEditor is ready to use!', editor);
        }}
        onChange={(event, editor) => {
          const data = editor.getData();
          console.log('📄 Nội dung:', data);
        }}
      />

      {/* Thêm script CDN vào */}
      <script src={editorUrl}></script>
    </div>
  );
}

export default MyEditor;