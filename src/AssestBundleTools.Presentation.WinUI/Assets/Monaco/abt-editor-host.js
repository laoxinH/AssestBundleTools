(function () {
  'use strict';

  const vscode = window.chrome && window.chrome.webview;
  let editor = null;
  let suppressChange = false;
  let currentLanguage = 'plaintext';
  let currentReadOnly = true;
  let currentTheme = 'light';

  function post(message) {
    if (vscode) {
      vscode.postMessage(message);
    }
  }

  function normalizeLanguage(language) {
    const value = (language || '').toLowerCase();
    if (value === 'json' || value === 'skel') {
      return 'json';
    }

    if (value === 'shader') {
      return 'cpp';
    }

    if (value === 'atlas') {
      return 'ini';
    }

    return 'plaintext';
  }

  function createEditor() {
    require.config({ paths: { vs: './vs' } });
    require(['vs/editor/editor.main'], function () {
      defineThemes();

      editor = monaco.editor.create(document.getElementById('editor'), {
        value: '',
        language: 'plaintext',
        theme: resolveThemeName(currentTheme),
        readOnly: true,
        automaticLayout: true,
        fontFamily: '"Cascadia Mono", Consolas, "Courier New", monospace',
        fontSize: 13,
        lineHeight: 20,
        lineNumbers: 'on',
        minimap: { enabled: true },
        glyphMargin: false,
        folding: true,
        scrollBeyondLastLine: false,
        renderLineHighlight: 'all',
        scrollbar: {
          verticalScrollbarSize: 12,
          horizontalScrollbarSize: 12,
          alwaysConsumeMouseWheel: false
        },
        find: {
          addExtraSpaceOnTop: false,
          autoFindInSelection: 'never',
          seedSearchStringFromSelection: 'selection'
        },
        wordWrap: 'off',
        tabSize: 4,
        insertSpaces: true
      });

      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, function () {
        post({
          type: 'saveRequested',
          text: editor.getValue()
        });
      });

      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyH, function () {
        editor.getAction('editor.action.startFindReplaceAction').run();
      });

      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyF, function () {
        editor.getAction('actions.find').run();
      });

      editor.onDidChangeModelContent(function () {
        if (suppressChange) {
          return;
        }

        post({ type: 'contentChanged' });
      });

      window.addEventListener('resize', function () {
        editor.layout();
      });

      post({ type: 'ready' });
    });
  }

  function defineThemes() {
    monaco.editor.defineTheme('abt-light', {
      base: 'vs',
      inherit: true,
      rules: [
        { token: 'string.key.json', foreground: '0f5f8a' },
        { token: 'string.value.json', foreground: '7a3f00' },
        { token: 'number', foreground: '0f6cbd' },
        { token: 'keyword', foreground: '8a2be2' }
      ],
      colors: {
        'editor.background': '#ffffff',
        'editor.foreground': '#1f1f1f',
        'editorLineNumber.foreground': '#8a8a8a',
        'editorLineNumber.activeForeground': '#d70021',
        'editorCursor.foreground': '#d70021',
        'editor.selectionBackground': '#add6ff',
        'editor.findMatchBackground': '#ffdf7e',
        'editor.findMatchHighlightBackground': '#fff2b8',
        'editor.lineHighlightBackground': '#f7f7f7',
        'editorGutter.background': '#ffffff'
      }
    });

    monaco.editor.defineTheme('abt-dark', {
      base: 'vs-dark',
      inherit: true,
      rules: [
        { token: 'string.key.json', foreground: '7dd3fc' },
        { token: 'string.value.json', foreground: 'fbbf24' },
        { token: 'number', foreground: '93c5fd' },
        { token: 'keyword', foreground: 'c084fc' }
      ],
      colors: {
        'editor.background': '#1f1f1f',
        'editor.foreground': '#f3f3f3',
        'editorLineNumber.foreground': '#858585',
        'editorLineNumber.activeForeground': '#ff99a8',
        'editorCursor.foreground': '#ff99a8',
        'editor.selectionBackground': '#264f78',
        'editor.findMatchBackground': '#8a6b1f',
        'editor.findMatchHighlightBackground': '#5f4b1a',
        'editor.lineHighlightBackground': '#2a2a2a',
        'editorGutter.background': '#1f1f1f'
      }
    });
  }

  function resolveThemeName(theme) {
    return theme === 'dark' ? 'abt-dark' : 'abt-light';
  }

  function setTheme(theme) {
    currentTheme = theme === 'dark' ? 'dark' : 'light';
    document.documentElement.style.background = currentTheme === 'dark' ? '#1f1f1f' : '#ffffff';
    document.body.style.background = currentTheme === 'dark' ? '#1f1f1f' : '#ffffff';
    if (editor) {
      monaco.editor.setTheme(resolveThemeName(currentTheme));
    }
  }

  function setDocument(payload) {
    if (!editor) {
      return;
    }

    const text = payload && typeof payload.text === 'string' ? payload.text : '';
    const language = normalizeLanguage(payload && payload.language);
    const readOnly = !!(payload && payload.readOnly);
    currentLanguage = language;
    currentReadOnly = readOnly;

    suppressChange = true;
    const model = editor.getModel();
    if (model) {
      monaco.editor.setModelLanguage(model, language);
      model.setValue(text);
    } else {
      editor.setModel(monaco.editor.createModel(text, language));
    }

    editor.updateOptions({
      readOnly,
      domReadOnly: readOnly
    });
    suppressChange = false;
    editor.layout();
  }

  function setReadOnly(readOnly) {
    currentReadOnly = !!readOnly;
    if (editor) {
      editor.updateOptions({
        readOnly: currentReadOnly,
        domReadOnly: currentReadOnly
      });
    }
  }

  function formatDocument() {
    if (!editor || currentReadOnly) {
      return;
    }

    editor.getAction('editor.action.formatDocument').run();
  }

  if (vscode) {
    vscode.addEventListener('message', function (event) {
      const message = event.data || {};
      switch (message.type) {
        case 'setDocument':
          setDocument(message);
          break;
        case 'setReadOnly':
          setReadOnly(message.readOnly);
          break;
        case 'setTheme':
          setTheme(message.theme);
          break;
        case 'focus':
          if (editor) {
            editor.focus();
          }
          break;
        case 'formatDocument':
          formatDocument();
          break;
        case 'getDocumentText':
          post({
            type: 'documentText',
            requestId: message.requestId,
            text: editor ? editor.getValue() : ''
          });
          break;
      }
    });
  }

  createEditor();
}());
