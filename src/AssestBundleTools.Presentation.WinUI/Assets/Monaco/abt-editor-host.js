(function () {
  'use strict';

  const vscode = window.chrome && window.chrome.webview;
  let editor = null;
  let suppressChange = false;
  let currentLanguage = 'plaintext';
  let currentReadOnly = true;
  let currentTheme = 'light';
  let baselineText = '';
  let baselineInfo = null;
  let isStreamingDocument = false;
  let streamingTextLength = 0;
  let streamingBaselineChunks = [];

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
          type: 'saveRequested'
        });
      });

      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyH, function () {
        editor.getAction('editor.action.startFindReplaceAction').run();
      });

      editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyF, function () {
        editor.getAction('actions.find').run();
      });

      editor.onDidChangeModelContent(function (event) {
        if (suppressChange) {
          return;
        }

        post({
          type: 'contentChanged',
          versionId: getModelVersionId()
        });
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

  function ensureModelUsesLf(model) {
    if (model) {
      model.setEOL(monaco.editor.EndOfLineSequence.LF);
    }
  }

  function setDocument(payload) {
    if (!editor) {
      return;
    }

    const text = normalizeText(payload && typeof payload.text === 'string' ? payload.text : '');
    const language = normalizeLanguage(payload && payload.language);
    const readOnly = !!(payload && payload.readOnly);
    currentLanguage = language;
    currentReadOnly = readOnly;
    baselineText = text;
    baselineInfo = null;
    isStreamingDocument = false;
    streamingTextLength = 0;
    streamingBaselineChunks = [];

    suppressChange = true;
    const model = editor.getModel();
    if (model) {
      monaco.editor.setModelLanguage(model, language);
      model.setValue(text);
      ensureModelUsesLf(model);
    } else {
      const nextModel = monaco.editor.createModel(text, language);
      ensureModelUsesLf(nextModel);
      editor.setModel(nextModel);
    }

    editor.updateOptions({
      readOnly,
      domReadOnly: readOnly
    });
    suppressChange = false;
    editor.layout();
    updateLargeDocumentOptions(text.length);
  }

  function beginStreamingDocument(payload) {
    if (!editor) {
      return;
    }

    const language = normalizeLanguage(payload && payload.language);
    const readOnly = !!(payload && payload.readOnly);
    currentLanguage = language;
    currentReadOnly = readOnly;
    baselineText = '';
    baselineInfo = null;
    isStreamingDocument = true;
    streamingTextLength = 0;
    streamingBaselineChunks = [];

    suppressChange = true;
    const model = editor.getModel();
    if (model) {
      monaco.editor.setModelLanguage(model, language);
      model.setValue('');
      ensureModelUsesLf(model);
    } else {
      const nextModel = monaco.editor.createModel('', language);
      ensureModelUsesLf(nextModel);
      editor.setModel(nextModel);
    }

    editor.updateOptions({
      readOnly,
      domReadOnly: readOnly
    });
    updateLargeDocumentOptions(Number(payload && payload.expectedLength) || 0);
    suppressChange = false;
    editor.layout();
  }

  function appendDocumentChunk(payload) {
    if (!editor || !isStreamingDocument) {
      return;
    }

    const model = editor.getModel();
    if (!model) {
      return;
    }

    const text = normalizeText(payload && typeof payload.text === 'string' ? payload.text : '');
    if (!text) {
      return;
    }

    suppressChange = true;
    ensureModelUsesLf(model);
    const lineCount = model.getLineCount();
    const endColumn = model.getLineMaxColumn(lineCount);
    model.applyEdits(
      [{
        range: new monaco.Range(lineCount, endColumn, lineCount, endColumn),
        text,
        forceMoveMarkers: true
      }],
      false);
    ensureModelUsesLf(model);
    suppressChange = false;
    streamingBaselineChunks.push(text);
    streamingTextLength += text.length;
  }

  function endStreamingDocument(payload) {
    if (!editor || !isStreamingDocument) {
      return;
    }

    baselineInfo = payload && payload.baseline
      ? {
          contentLength: Number(payload.baseline.contentLength) || 0,
          contentSha256: String(payload.baseline.contentSha256 || '')
        }
      : null;
    baselineText = streamingBaselineChunks.join('');
    streamingBaselineChunks = [];
    isStreamingDocument = false;
    streamingTextLength = baselineInfo ? baselineInfo.contentLength : baselineText.length;
    currentReadOnly = !!(payload && payload.readOnly);
    editor.updateOptions({
      readOnly: currentReadOnly,
      domReadOnly: currentReadOnly
    });
    updateLargeDocumentOptions(streamingTextLength);
  }

  function normalizeText(text) {
    return (text || '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
  }

  function getEditorText() {
    const model = editor && editor.getModel();
    if (!model) {
      return '';
    }

    return normalizeText(model.getValue(monaco.editor.EndOfLinePreference.LF));
  }

  function getModelVersionId() {
    const model = editor && editor.getModel();
    return model ? model.getVersionId() : 0;
  }

  function getPendingRangeEdits() {
    if (!editor || currentReadOnly) {
      return [];
    }

    const currentText = getEditorText();
    if (baselineInfo && baselineText.length !== baselineInfo.contentLength) {
      return [{
        start: 0,
        length: baselineInfo.contentLength,
        text: currentText
      }];
    }

    return createRangeEditFromTexts(baselineText, currentText);
  }

  function createRangeEditFromTexts(savedText, currentText) {
    if (currentText === savedText) {
      return [];
    }

    const maxPrefixLength = Math.min(savedText.length, currentText.length);
    let prefixLength = 0;
    while (
      prefixLength < maxPrefixLength &&
      savedText.charCodeAt(prefixLength) === currentText.charCodeAt(prefixLength)
    ) {
      prefixLength++;
    }

    let baselineEnd = savedText.length;
    let currentEnd = currentText.length;
    while (
      baselineEnd > prefixLength &&
      currentEnd > prefixLength &&
      savedText.charCodeAt(baselineEnd - 1) === currentText.charCodeAt(currentEnd - 1)
    ) {
      baselineEnd--;
      currentEnd--;
    }

    return [{
      start: prefixLength,
      length: baselineEnd - prefixLength,
      text: currentText.slice(prefixLength, currentEnd)
    }];
  }

  function markSaved() {
    if (!editor) {
      baselineText = '';
      baselineInfo = null;
      streamingBaselineChunks = [];
      return;
    }

    baselineText = getEditorText();
    baselineInfo = null;
    streamingBaselineChunks = [];
  }

  function updateLargeDocumentOptions(length) {
    if (!editor) {
      return;
    }

    const isLarge = length >= 2 * 1024 * 1024;
    editor.updateOptions({
      minimap: { enabled: !isLarge },
      folding: !isLarge,
      renderWhitespace: 'none',
      occurrencesHighlight: !isLarge,
      selectionHighlight: !isLarge,
      links: !isLarge,
      codeLens: false
    });
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
        case 'beginStreamingDocument':
          beginStreamingDocument(message);
          break;
        case 'appendDocumentChunk':
          appendDocumentChunk(message);
          break;
        case 'endStreamingDocument':
          endStreamingDocument(message);
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
        case 'markSaved':
          if (message.baseline) {
            baselineText = getEditorText();
            baselineInfo = {
              contentLength: Number(message.baseline.contentLength) || 0,
              contentSha256: String(message.baseline.contentSha256 || '')
            };
            streamingTextLength = baselineInfo.contentLength;
            streamingBaselineChunks = [];
          } else {
            markSaved();
          }
          break;
        case 'getPendingRangeEdits':
          post({
            type: 'documentRangeEdits',
            requestId: message.requestId,
            baseLength: baselineInfo ? baselineInfo.contentLength : baselineText.length,
            versionId: getModelVersionId(),
            edits: getPendingRangeEdits()
          });
          break;
        case 'getDocumentText':
          post({
            type: 'documentText',
            requestId: message.requestId,
            text: editor ? getEditorText() : ''
          });
          break;
      }
    });
  }

  createEditor();
}());
