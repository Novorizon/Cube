$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$docsRoot = Resolve-Path (Join-Path $scriptRoot '..')
$outputPath = Join-Path $scriptRoot 'index.html'

$docSpecs = @(
    [ordered]@{ Group = '入口'; Title = '文档总入口'; Path = 'README.md'; Summary = '项目文档总入口和按任务选读指南。' },
    [ordered]@{ Group = '入口'; Title = 'Codex 项目记忆'; Path = 'CodexProjectMemory.md'; Summary = '给 Codex 新对话使用的最小项目记忆。' },

    [ordered]@{ Group = '架构'; Title = '代码库概览'; Path = 'Architecture/CodebaseOverview.md'; Summary = '代码分层、目录边界和 Unity 安全规则。' },
    [ordered]@{ Group = '架构'; Title = '模块工作索引'; Path = 'Architecture/ModuleWorkMap.md'; Summary = '模块对应的文档、代码、配置表、工具和生成物总览。' },
    [ordered]@{ Group = '架构'; Title = 'UI 框架'; Path = 'Architecture/UIFramework.md'; Summary = 'UI 类型、关闭规则、Stack、Exclusive 和 prefabPath 语义。' },
    [ordered]@{ Group = '架构'; Title = '数据流水线'; Path = 'Architecture/DataPipeline.md'; Summary = 'Luban、Excel、本地化和生成数据规则。' },
    [ordered]@{ Group = '架构'; Title = 'Excel 数据字典'; Path = 'Architecture/ExcelDataDictionary.md'; Summary = '每个 Excel、每个字段、表间关系和当前接入状态。' },
    [ordered]@{ Group = '架构'; Title = '地图运行时'; Path = 'Architecture/MapRuntime.md'; Summary = '地图运行时生成、MapData.Objects 和地图标记边界。' },
    [ordered]@{ Group = '架构'; Title = '存档系统'; Path = 'Architecture/SaveSystem.md'; Summary = '存档边界和 Calendar 存档规则。' },
    [ordered]@{ Group = '架构'; Title = '消息系统'; Path = 'Architecture/Messaging.md'; Summary = '消息系统使用边界和 Quest 消息。' },

    [ordered]@{ Group = '产品'; Title = '游戏概览'; Path = 'Product/GameOverview.md'; Summary = '项目整体方向和经营 / 塔防模式关系。' },
    [ordered]@{ Group = '产品'; Title = '经营模式'; Path = 'Product/ManagementMode.md'; Summary = '岛屿经营模式的产品范围和当前缺口。' },
    [ordered]@{ Group = '产品'; Title = '塔防模式'; Path = 'Product/TowerDefenseMode.md'; Summary = '塔防模式范围和与经营模式的边界。' },
    [ordered]@{ Group = '产品'; Title = '成长与任务'; Path = 'Product/ProgressionAndQuests.md'; Summary = '开局、任务、剧情和奖励流程。' },

    [ordered]@{ Group = '模块'; Title = '岛屿经营模块'; Path = 'Modules/Island.md'; Summary = '岛屿经营业务模块、工具、农田、建筑和 Calendar。' },
    [ordered]@{ Group = '模块'; Title = '经营 UI'; Path = 'Modules/ManagementUI.md'; Summary = '经营主界面、浮动面板、入口互斥和布局规则。' },
    [ordered]@{ Group = '模块'; Title = '剧情模块'; Path = 'Modules/Story.md'; Summary = '剧情播放、触发、存档和 StoryPanel 当前实现。' },
    [ordered]@{ Group = '模块'; Title = '任务与蓝图'; Path = 'Modules/QuestStoryBlueprint.md'; Summary = '任务、蓝图、Quest UI 以及和 Story 的衔接。' },
    [ordered]@{ Group = '模块'; Title = 'Ability 与 Skill'; Path = 'Modules/AbilityAndSkill.md'; Summary = '技能、Buff、投射物和兼容配置的当前状态。' },
    [ordered]@{ Group = '模块'; Title = '地图与地块美术'; Path = 'Modules/MapAndTileArt.md'; Summary = '地图地块美术当前方向和不继续的旧方案。' },
    [ordered]@{ Group = '模块'; Title = '塔防模块'; Path = 'Modules/TowerDefense.md'; Summary = '塔防业务模块入口。' },

    [ordered]@{ Group = '决策'; Title = '0001 命名规则'; Path = 'Decisions/0001-naming-rules.md'; Summary = '命名规则：新代码默认不加 World / Game。' },
    [ordered]@{ Group = '决策'; Title = '0002 UI 类型使用'; Path = 'Decisions/0002-ui-kind-usage.md'; Summary = 'Page、Panel、Popup、Overlay、Toast 的使用决策。' },
    [ordered]@{ Group = '决策'; Title = '0003 地图对象与标记'; Path = 'Decisions/0003-map-object-vs-marker.md'; Summary = '地图真实对象与 UI 标记的边界。' },

    [ordered]@{ Group = '审核'; Title = '项目中期审核'; Path = 'Audits/ProjectMidpointAudit-2026-07-09.md'; Summary = '2026-07-09 中期代码、文档、配置和工具审核。' }
)

$docs = foreach ($spec in $docSpecs) {
    $relativePath = $spec.Path
    $fullPath = Join-Path $docsRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "Document not found: $relativePath"
    }

    $id = ($relativePath -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant()
    [ordered]@{
        id = $id
        group = $spec.Group
        title = $spec.Title
        path = $relativePath
        summary = $spec.Summary
        content = [System.IO.File]::ReadAllText($fullPath, [System.Text.Encoding]::UTF8)
    }
}

$docsJson = $docs | ConvertTo-Json -Depth 8

$htmlTemplate = @'
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Cube 项目文档</title>
  <style>
    :root {
      --bg: #f6f4ef;
      --panel: #ffffff;
      --panel-soft: #fbfaf7;
      --text: #202124;
      --muted: #667085;
      --line: #ded8cb;
      --blue: #315fbd;
      --teal: #0f766e;
      --amber: #b45309;
      --shadow: 0 18px 40px rgba(32, 33, 36, .08);
    }

    * { box-sizing: border-box; }

    html { scroll-behavior: smooth; }

    body {
      margin: 0;
      min-height: 100vh;
      color: var(--text);
      background: var(--bg);
      font-family: "Microsoft YaHei", "Segoe UI", system-ui, sans-serif;
      line-height: 1.65;
    }

    button, input {
      font: inherit;
    }

    .app-shell {
      display: grid;
      grid-template-columns: 320px minmax(0, 1fr);
      min-height: 100vh;
    }

    .sidebar {
      position: sticky;
      top: 0;
      height: 100vh;
      display: flex;
      flex-direction: column;
      border-right: 1px solid var(--line);
      background: #fdfcf9;
    }

    .brand {
      padding: 22px 22px 16px;
      border-bottom: 1px solid var(--line);
    }

    .brand h1 {
      margin: 0;
      font-size: 24px;
      line-height: 1.2;
      letter-spacing: 0;
    }

    .brand p {
      margin: 8px 0 0;
      color: var(--muted);
      font-size: 13px;
    }

    .search-area {
      padding: 14px 16px;
      border-bottom: 1px solid var(--line);
    }

    .search-input {
      width: 100%;
      min-height: 40px;
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 9px 12px;
      color: var(--text);
      background: var(--panel);
      outline: none;
    }

    .search-input:focus {
      border-color: var(--blue);
      box-shadow: 0 0 0 3px rgba(49, 95, 189, .13);
    }

    .nav-scroll {
      flex: 1;
      overflow: auto;
      padding: 10px 12px 18px;
    }

    .nav-group {
      margin: 10px 0 16px;
    }

    .nav-group-title {
      display: flex;
      align-items: center;
      justify-content: space-between;
      min-height: 40px;
      margin: 0 4px 8px;
      border-left: 4px solid var(--teal);
      border-radius: 6px;
      padding: 8px 10px;
      color: var(--text);
      background: #f1ede4;
      font-size: 16px;
      font-weight: 800;
      letter-spacing: 0;
    }

    .nav-count {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 25px;
      height: 22px;
      border: 1px solid #d8d0c2;
      border-radius: 11px;
      padding: 0 7px;
      color: var(--muted);
      background: var(--panel);
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0;
    }

    .doc-button {
      display: block;
      width: 100%;
      border: 0;
      border-radius: 8px;
      padding: 10px 10px;
      margin: 3px 0;
      text-align: left;
      background: transparent;
      color: var(--text);
      cursor: pointer;
    }

    .doc-nav-entry {
      position: relative;
    }

    .doc-nav-entry.has-outline .doc-button {
      padding-right: 42px;
    }

    .doc-outline-toggle {
      position: absolute;
      z-index: 1;
      top: 7px;
      right: 5px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 30px;
      height: 30px;
      border: 0;
      border-radius: 6px;
      color: var(--muted);
      background: transparent;
      font-size: 22px;
      line-height: 1;
      cursor: pointer;
      transform: rotate(0deg);
      transition: transform .16s ease, background .16s ease;
    }

    .doc-outline-toggle:hover {
      color: var(--blue);
      background: rgba(49, 95, 189, .1);
    }

    .doc-nav-entry.outline-expanded .doc-outline-toggle {
      transform: rotate(90deg);
    }

    .doc-outline {
      display: none;
      margin: 2px 8px 8px 17px;
      border-left: 1px solid #d8d0c2;
      padding: 2px 0 3px 10px;
    }

    .doc-nav-entry.outline-expanded .doc-outline {
      display: block;
    }

    .doc-heading-button {
      display: block;
      width: 100%;
      border: 0;
      border-radius: 5px;
      padding: 5px 7px;
      color: var(--muted);
      background: transparent;
      font-size: 12px;
      line-height: 1.35;
      letter-spacing: 0;
      text-align: left;
      overflow-wrap: anywhere;
      cursor: pointer;
    }

    .doc-heading-button:hover {
      color: var(--text);
      background: #f1ede4;
    }

    .doc-heading-button.active {
      color: #173b82;
      background: #eaf0ff;
      font-weight: 700;
    }

    .doc-button:hover {
      background: #f1ede4;
    }

    .doc-button.active {
      background: #eaf0ff;
      color: #173b82;
      box-shadow: inset 3px 0 0 var(--blue);
    }

    .doc-button strong {
      display: block;
      font-size: 14px;
      line-height: 1.35;
      letter-spacing: 0;
    }

    .doc-button span {
      display: block;
      margin-top: 3px;
      color: var(--muted);
      font-size: 12px;
      line-height: 1.45;
    }

    .main {
      min-width: 0;
      padding: 24px clamp(20px, 4vw, 52px) 60px;
    }

    .mobile-topbar {
      display: none;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 16px;
    }

    .menu-button,
    .small-button {
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--panel);
      color: var(--text);
      min-height: 36px;
      padding: 7px 12px;
      cursor: pointer;
    }

    .menu-button:hover,
    .small-button:hover {
      border-color: #b8b0a2;
      background: #fdfaf3;
    }

    .doc-header {
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: 18px;
      align-items: start;
      margin: 0 auto 18px;
      max-width: 1120px;
    }

    .eyebrow {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      color: var(--teal);
      font-size: 13px;
      font-weight: 700;
    }

    .doc-title {
      margin: 4px 0 8px;
      font-size: clamp(28px, 4vw, 44px);
      line-height: 1.15;
      letter-spacing: 0;
    }

    .doc-summary {
      margin: 0;
      max-width: 760px;
      color: var(--muted);
      font-size: 16px;
    }

    .source-actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: flex-end;
      gap: 8px;
    }

    .source-path {
      display: inline-flex;
      align-items: center;
      min-height: 34px;
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 6px 10px;
      color: var(--muted);
      background: rgba(255,255,255,.7);
      font-size: 12px;
      white-space: nowrap;
    }

    .content-layout {
      display: grid;
      grid-template-columns: minmax(0, 860px) minmax(190px, 240px);
      gap: 28px;
      align-items: start;
      max-width: 1120px;
      margin: 0 auto;
    }

    .reader {
      min-width: 0;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--panel);
      box-shadow: var(--shadow);
      padding: clamp(22px, 3vw, 36px);
    }

    .toc {
      position: sticky;
      top: 22px;
      max-height: calc(100vh - 44px);
      overflow: auto;
      border-left: 2px solid var(--line);
      padding-left: 14px;
      color: var(--muted);
      font-size: 13px;
    }

    .toc-title {
      margin: 0 0 8px;
      color: var(--text);
      font-weight: 700;
    }

    .toc a {
      display: block;
      padding: 4px 0;
      color: var(--muted);
      text-decoration: none;
    }

    .toc a:hover {
      color: var(--blue);
    }

    .toc a.level-3 {
      padding-left: 12px;
      font-size: 12px;
    }

    .reader h1,
    .reader h2,
    .reader h3,
    .reader h4 {
      line-height: 1.3;
      letter-spacing: 0;
    }

    .reader h1 {
      margin: 0 0 18px;
      font-size: 32px;
    }

    .reader h2 {
      margin: 34px 0 12px;
      padding-top: 4px;
      font-size: 24px;
      border-top: 1px solid #eee8db;
    }

    .reader h3 {
      margin: 26px 0 10px;
      font-size: 19px;
    }

    .reader h4 {
      margin: 22px 0 8px;
      font-size: 16px;
    }

    .table-index {
      margin: 18px 0 28px;
      border: 1px solid var(--line);
      border-radius: 8px;
      background: var(--panel-soft);
    }

    .table-index-header {
      padding: 14px 16px 10px;
      border-bottom: 1px solid var(--line);
    }

    .table-index-title {
      margin: 0;
      font-size: 18px;
      font-weight: 700;
    }

    .table-index-note {
      margin: 3px 0 0;
      color: var(--muted);
      font-size: 13px;
    }

    .table-index-table-wrap {
      overflow-x: auto;
    }

    .table-index-table {
      min-width: 560px;
      border-collapse: collapse;
      background: var(--panel);
    }

    .table-index-table thead th {
      color: var(--text);
      background: #eae4d8;
      font-size: 13px;
    }

    .table-index-table tbody th {
      width: 180px;
      color: var(--text);
      background: #f7f3eb;
      font-size: 13px;
      font-weight: 700;
      white-space: nowrap;
    }

    .table-index-links {
      display: flex;
      flex-wrap: wrap;
      gap: 5px 14px;
    }

    .reader .table-index-link {
      display: inline-block;
      border-bottom: 0;
      color: var(--blue);
      font-family: Consolas, "Cascadia Mono", monospace;
      font-size: 12px;
      overflow-wrap: anywhere;
    }

    .reader .table-index-link:hover {
      border-bottom: 0;
      text-decoration: underline;
    }

    .table-section {
      margin: 18px 0 22px;
      border: 1px solid var(--line);
      border-left: 4px solid var(--teal);
      border-radius: 8px;
      background: var(--panel);
      scroll-margin-top: 18px;
    }

    .table-section > summary {
      display: flex;
      align-items: center;
      gap: 10px;
      min-height: 52px;
      padding: 12px 15px;
      cursor: pointer;
      list-style: none;
      background: #f4efe5;
      font-size: 18px;
      font-weight: 700;
    }

    .table-section > summary::-webkit-details-marker {
      display: none;
    }

    .table-section > summary::before {
      content: "›";
      flex: 0 0 auto;
      color: var(--teal);
      font-size: 25px;
      line-height: 1;
      transform: rotate(0deg);
      transition: transform .16s ease;
    }

    .table-section[open] > summary {
      border-bottom: 1px solid var(--line);
    }

    .table-section[open] > summary::before {
      transform: rotate(90deg);
    }

    .table-section-title {
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .table-section-hint {
      margin-left: auto;
      color: var(--muted);
      font-size: 12px;
      font-weight: 400;
    }

    .table-section-hint::after {
      content: "展开";
    }

    .table-section[open] .table-section-hint::after {
      content: "收起";
    }

    .table-section-body {
      padding: 8px 16px 12px;
    }

    .table-section-body > p:first-child {
      margin-top: 8px;
      padding-bottom: 10px;
      border-bottom: 1px solid #eee8db;
    }

    .table-section-body > p:first-child::before {
      content: "说明";
      display: inline-block;
      margin-right: 8px;
      color: var(--teal);
      font-size: 12px;
      font-weight: 700;
    }

    .reader p {
      margin: 10px 0;
    }

    .reader .definition {
      margin: -6px 0 14px 0;
      color: var(--muted);
    }

    .reader ul,
    .reader ol {
      padding-left: 1.25rem;
      margin: 10px 0 14px;
    }

    .reader li {
      margin: 4px 0;
    }

    .reader a {
      color: var(--blue);
      text-decoration: none;
      border-bottom: 1px solid rgba(49, 95, 189, .25);
    }

    .reader a:hover {
      border-bottom-color: var(--blue);
    }

    .reader code {
      border-radius: 5px;
      padding: 2px 5px;
      background: #f2eee6;
      color: #5d3b00;
      font-family: Consolas, "Cascadia Mono", monospace;
      font-size: .92em;
    }

    .reader pre {
      overflow: auto;
      border: 1px solid #e1d8c8;
      border-radius: 8px;
      padding: 14px 16px;
      background: #f2eee6;
      color: #5d3b00;
      line-height: 1.55;
    }

    .reader pre code {
      padding: 0;
      color: inherit;
      background: transparent;
      font-size: 13px;
    }

    .table-wrap {
      overflow-x: auto;
      margin: 14px 0 18px;
      border: 1px solid var(--line);
      border-radius: 8px;
    }

    table {
      width: 100%;
      border-collapse: collapse;
      min-width: 560px;
      background: var(--panel);
    }

    th,
    td {
      border-bottom: 1px solid #ebe4d8;
      padding: 9px 11px;
      text-align: left;
      vertical-align: top;
    }

    th {
      background: #f4efe5;
      font-weight: 700;
    }

    tr:last-child td {
      border-bottom: 0;
    }

    td.merged-group-cell {
      width: 150px;
      vertical-align: middle;
      color: var(--text);
      background: #faf7f1;
      font-weight: 700;
    }

    blockquote {
      margin: 16px 0;
      border-left: 4px solid var(--amber);
      padding: 8px 14px;
      color: #5f4b32;
      background: #fff7e7;
    }

    mark {
      background: #fde68a;
      color: inherit;
      padding: 0 2px;
      border-radius: 3px;
    }

    .empty-state {
      padding: 24px;
      color: var(--muted);
      text-align: center;
    }

    @media (max-width: 980px) {
      .app-shell {
        grid-template-columns: 1fr;
      }

      .sidebar {
        position: fixed;
        z-index: 20;
        inset: 0 auto 0 0;
        width: min(86vw, 340px);
        transform: translateX(-105%);
        transition: transform .2s ease;
        box-shadow: 20px 0 40px rgba(0,0,0,.16);
      }

      body.nav-open .sidebar {
        transform: translateX(0);
      }

      .mobile-topbar {
        display: flex;
      }

      .main {
        padding-top: 16px;
      }

      .doc-header {
        grid-template-columns: 1fr;
      }

      .source-actions {
        justify-content: flex-start;
      }

      .content-layout {
        grid-template-columns: 1fr;
      }

      .toc {
        position: static;
        border-left: 0;
        border-top: 1px solid var(--line);
        padding: 14px 0 0;
        order: -1;
      }
    }

    @media (max-width: 640px) {
      .table-section > summary {
        align-items: flex-start;
      }

      .table-section-body {
        padding-inline: 10px;
      }
    }
  </style>
</head>
<body>
  <div class="app-shell">
    <aside class="sidebar" aria-label="文档导航">
      <div class="brand">
        <h1>Cube 项目文档</h1>
        <p>Markdown 作为数据源，HTML 提供阅读界面。</p>
      </div>
      <div class="search-area">
        <input id="searchInput" class="search-input" type="search" placeholder="搜索文档标题、路径和内容" autocomplete="off">
      </div>
      <nav id="navList" class="nav-scroll"></nav>
    </aside>

    <main class="main">
      <div class="mobile-topbar">
        <button id="openNavButton" class="menu-button" type="button">目录</button>
        <span class="source-path">Docs/Human/index.html</span>
      </div>

      <header class="doc-header">
        <div>
          <div id="docGroup" class="eyebrow"></div>
          <h2 id="docTitle" class="doc-title"></h2>
          <p id="docSummary" class="doc-summary"></p>
        </div>
        <div class="source-actions">
          <span id="docPath" class="source-path"></span>
          <a id="rawLink" class="small-button" href="#" target="_blank" rel="noreferrer">Markdown 源文件</a>
        </div>
      </header>

      <section class="content-layout">
        <article id="reader" class="reader"></article>
        <aside id="toc" class="toc" aria-label="本文目录"></aside>
      </section>
    </main>
  </div>

  <script>
    const docs = __DOCS_JSON__;

    const navList = document.getElementById('navList');
    const searchInput = document.getElementById('searchInput');
    const reader = document.getElementById('reader');
    const toc = document.getElementById('toc');
    const docGroup = document.getElementById('docGroup');
    const docTitle = document.getElementById('docTitle');
    const docSummary = document.getElementById('docSummary');
    const docPath = document.getElementById('docPath');
    const rawLink = document.getElementById('rawLink');
    const openNavButton = document.getElementById('openNavButton');

    let activeDoc = docs[0];
    let activeHeading = '';
    const expandedDocOutlines = new Set();
    const docHeadingCache = new Map();

    function escapeHtml(value) {
      return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
    }

    function escapeAttr(value) {
      return escapeHtml(value).replace(/`/g, '&#96;');
    }

    function renderInline(text) {
      let html = escapeHtml(text);
      html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, label, href) => {
        return `<a href="${escapeAttr(href)}">${label}</a>`;
      });
      html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
      html = html.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
      return html;
    }

    function isTableLine(line) {
      const trimmed = line.trim();
      return trimmed.startsWith('|') && trimmed.endsWith('|');
    }

    function isSeparatorRow(line) {
      return /^\|?[\s:-]+\|[\s|:-]*$/.test(line.trim());
    }

    function splitTableRow(line) {
      return line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(cell => cell.trim());
    }

    function markdownToHtml(markdown, options = {}) {
      const lines = markdown.replace(/\r\n/g, '\n').split('\n');
      const html = [];
      const headings = [];
      const collapsibleLevel3 = Boolean(options.collapsibleLevel3);
      let paragraph = [];
      let listType = '';
      let tableRows = [];
      let inCode = false;
      let codeLines = [];
      let codeLang = '';
      let detailsOpen = false;
      let currentSection = '';

      function flushParagraph() {
        if (!paragraph.length) return;
        html.push(`<p>${renderInline(paragraph.join(' '))}</p>`);
        paragraph = [];
      }

      function closeList() {
        if (!listType) return;
        html.push(`</${listType}>`);
        listType = '';
      }

      function flushTable() {
        if (!tableRows.length) return;
        const rows = tableRows.filter(row => !isSeparatorRow(row)).map(splitTableRow);
        tableRows = [];
        if (!rows.length) return;
        const head = rows[0];
        const body = rows.slice(1);
        const thead = `<thead><tr>${head.map(cell => `<th>${renderInline(cell)}</th>`).join('')}</tr></thead>`;
        const mergeFirstColumn = head.length === 3 &&
          head[0] === '分类' &&
          head[1] === '来源字段' &&
          head[2] === '关联目标';
        let bodyHtml = '';

        if (mergeFirstColumn) {
          for (let rowIndex = 0; rowIndex < body.length; rowIndex += 1) {
            const row = body[rowIndex];
            let firstCell = '';
            if (row[0]) {
              let rowSpan = 1;
              while (rowIndex + rowSpan < body.length && !body[rowIndex + rowSpan][0]) rowSpan += 1;
              firstCell = `<td class="merged-group-cell" rowspan="${rowSpan}">${renderInline(row[0])}</td>`;
            }

            bodyHtml += `<tr>${firstCell}<td>${renderInline(row[1] || '')}</td><td>${renderInline(row[2] || '')}</td></tr>`;
          }
        } else {
          bodyHtml = body.map(row => `<tr>${row.map(cell => `<td>${renderInline(cell)}</td>`).join('')}</tr>`).join('');
        }

        const tbody = `<tbody>${bodyHtml}</tbody>`;
        html.push(`<div class="table-wrap"><table>${thead}${tbody}</table></div>`);
      }

      function closeDetails() {
        if (!detailsOpen) return;
        html.push('</div></details>');
        detailsOpen = false;
      }

      for (let index = 0; index < lines.length; index += 1) {
        const line = lines[index];
        const trimmed = line.trim();

        if (trimmed.startsWith('```')) {
          if (inCode) {
            html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
            inCode = false;
            codeLines = [];
            codeLang = '';
          } else {
            flushParagraph();
            closeList();
            flushTable();
            inCode = true;
            codeLang = trimmed.slice(3).trim();
          }
          continue;
        }

        if (inCode) {
          codeLines.push(line);
          continue;
        }

        if (!trimmed) {
          flushParagraph();
          closeList();
          flushTable();
          continue;
        }

        if (isTableLine(line)) {
          flushParagraph();
          closeList();
          tableRows.push(line);
          const next = lines[index + 1] || '';
          if (!isTableLine(next)) flushTable();
          continue;
        }

        const heading = line.match(/^(#{1,6})\s+(.+)$/);
        if (heading) {
          flushParagraph();
          closeList();
          flushTable();
          const level = heading[1].length;
          const text = heading[2].trim();
          const id = `heading-${headings.length + 1}`;
          if (level <= 3) closeDetails();
          if (level === 2) currentSection = text;
          if (level <= 3) headings.push({ id, level, text, section: currentSection });

          if (collapsibleLevel3 && level === 3) {
            html.push(`<details class="table-section" id="${id}"><summary><span class="table-section-title">${renderInline(text)}</span><span class="table-section-hint" aria-hidden="true"></span></summary><div class="table-section-body">`);
            detailsOpen = true;
            continue;
          }

          html.push(`<h${level} id="${id}">${renderInline(text)}</h${level}>`);
          continue;
        }

        const bullet = line.match(/^\s*-\s+(.+)$/);
        const numbered = line.match(/^\s*\d+\.\s+(.+)$/);
        if (bullet || numbered) {
          flushParagraph();
          flushTable();
          const type = bullet ? 'ul' : 'ol';
          if (listType && listType !== type) closeList();
          if (!listType) {
            listType = type;
            html.push(`<${type}>`);
          }
          html.push(`<li>${renderInline((bullet || numbered)[1])}</li>`);
          continue;
        }

        const quote = line.match(/^\s*>\s+(.+)$/);
        if (quote) {
          flushParagraph();
          closeList();
          flushTable();
          html.push(`<blockquote>${renderInline(quote[1])}</blockquote>`);
          continue;
        }

        const definition = line.match(/^:\s+(.+)$/);
        if (definition) {
          flushParagraph();
          closeList();
          flushTable();
          html.push(`<p class="definition">${renderInline(definition[1])}</p>`);
          continue;
        }

        paragraph.push(trimmed);
      }

      if (inCode) html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`);
      flushParagraph();
      closeList();
      flushTable();
      closeDetails();

      return { html: html.join('\n'), headings };
    }

    function renderTableIndex(headings) {
      const tableHeadings = headings.filter(item => item.level === 3);
      if (!tableHeadings.length) return '';

      const groups = tableHeadings.reduce((result, item) => {
        const section = item.section || '其他';
        if (!result.has(section)) result.set(section, []);
        result.get(section).push(item);
        return result;
      }, new Map());

      return `
        <nav class="table-index" aria-label="Excel 表目录">
          <div class="table-index-header">
            <p class="table-index-title">表目录</p>
            <p class="table-index-note">按业务查找配置表，点击表名后自动展开。</p>
          </div>
          <div class="table-index-table-wrap">
            <table class="table-index-table">
              <thead>
                <tr><th>分类</th><th>具体配置表</th></tr>
              </thead>
              <tbody>
                ${Array.from(groups.entries()).map(([section, items]) => `
                  <tr>
                    <th scope="row">${renderInline(section)}</th>
                    <td><div class="table-index-links">
                      ${items.map(item => `<a class="table-index-link" href="#${activeDoc.id}:${item.id}">${renderInline(item.text)}</a>`).join('')}
                    </div></td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>
        </nav>
      `;
    }

    function groupDocs(items) {
      return items.reduce((groups, doc) => {
        if (!groups.has(doc.group)) groups.set(doc.group, []);
        groups.get(doc.group).push(doc);
        return groups;
      }, new Map());
    }

    function matchesQuery(doc, query) {
      if (!query) return true;
      const haystack = `${doc.title}\n${doc.path}\n${doc.summary}\n${doc.content}`.toLowerCase();
      return haystack.includes(query.toLowerCase());
    }

    function getDocumentHeadings(doc) {
      if (!docHeadingCache.has(doc.id)) {
        docHeadingCache.set(doc.id, markdownToHtml(doc.content).headings);
      }

      return docHeadingCache.get(doc.id).filter(item => item.level === 2);
    }

    function renderNav() {
      const query = searchInput.value.trim();
      const filtered = docs.filter(doc => matchesQuery(doc, query));
      const groups = groupDocs(filtered);

      if (!filtered.length) {
        navList.innerHTML = '<div class="empty-state">没有匹配的文档</div>';
        return;
      }

      navList.innerHTML = Array.from(groups.entries()).map(([group, items]) => `
        <section class="nav-group">
          <div class="nav-group-title">${escapeHtml(group)} <span class="nav-count">${items.length}</span></div>
          <div class="nav-group-items">
            ${items.map(doc => {
              const headings = getDocumentHeadings(doc);
              const expanded = expandedDocOutlines.has(doc.id);
              return `
            <div class="doc-nav-entry ${headings.length ? 'has-outline' : ''} ${expanded ? 'outline-expanded' : ''}">
              <button class="doc-button ${doc.id === activeDoc.id ? 'active' : ''}" type="button" data-doc-id="${doc.id}">
                <strong>${escapeHtml(doc.title)}</strong>
                <span>${escapeHtml(doc.summary)}</span>
              </button>
              ${headings.length ? `
                <button class="doc-outline-toggle" type="button" data-doc-outline-toggle="${doc.id}" aria-label="${expanded ? '收起' : '展开'} ${escapeAttr(doc.title)} 的标题" aria-expanded="${String(expanded)}" title="${expanded ? '收起标题' : '展开标题'}">›</button>
                <div class="doc-outline">
                  ${headings.map(heading => `
                    <button class="doc-heading-button level-${heading.level} ${doc.id === activeDoc.id && heading.id === activeHeading ? 'active' : ''}" type="button" data-doc-id="${doc.id}" data-heading-id="${heading.id}">${escapeHtml(heading.text)}</button>
                  `).join('')}
                </div>
              ` : ''}
            </div>
            `;
            }).join('')}
          </div>
        </section>
      `).join('');
    }

    function renderToc(headings) {
      if (!headings.length) {
        toc.innerHTML = '<p class="toc-title">本文目录</p><p>没有可用标题</p>';
        return;
      }

      toc.innerHTML = `
        <p class="toc-title">本文目录</p>
        ${headings.map(item => `<a class="level-${item.level}" href="#${activeDoc.id}:${item.id}">${escapeHtml(item.text)}</a>`).join('')}
      `;
    }

    function renderDocument(doc, headingId = '') {
      activeDoc = doc;
      activeHeading = headingId;
      expandedDocOutlines.add(doc.id);
      const isExcelDictionary = doc.path === 'Architecture/ExcelDataDictionary.md';
      const result = markdownToHtml(doc.content, { collapsibleLevel3: isExcelDictionary });
      docHeadingCache.set(doc.id, result.headings);
      docGroup.textContent = `${doc.group} / ${doc.path}`;
      docTitle.textContent = doc.title;
      docSummary.textContent = doc.summary;
      docPath.textContent = doc.path;
      rawLink.href = `../${doc.path}`;
      reader.innerHTML = result.html;
      if (isExcelDictionary) {
        const firstSection = reader.querySelector('h2');
        if (firstSection) firstSection.insertAdjacentHTML('beforebegin', renderTableIndex(result.headings));
      }
      renderToc(isExcelDictionary ? result.headings.filter(item => item.level <= 2) : result.headings);
      renderNav();

      if (headingId) {
        requestAnimationFrame(() => {
          const target = document.getElementById(headingId);
          if (target) {
            if (target.matches('details')) target.open = true;
            const parentDetails = target.closest('details');
            if (parentDetails) parentDetails.open = true;
            target.scrollIntoView({ block: 'start' });
          }
        });
      } else {
        window.scrollTo({ top: 0, behavior: 'auto' });
      }
    }

    function findDoc(id) {
      return docs.find(doc => doc.id === id) || docs[0];
    }

    function parseHash() {
      const raw = decodeURIComponent(location.hash.replace(/^#/, ''));
      const [docId, headingId] = raw.split(':');
      return { docId, headingId };
    }

    navList.addEventListener('click', event => {
      const outlineButton = event.target.closest('[data-doc-outline-toggle]');
      if (outlineButton) {
        const docId = outlineButton.dataset.docOutlineToggle;
        const entry = outlineButton.closest('.doc-nav-entry');
        const expanded = entry.classList.toggle('outline-expanded');
        outlineButton.setAttribute('aria-expanded', String(expanded));
        outlineButton.setAttribute('aria-label', `${expanded ? '收起' : '展开'}标题`);
        outlineButton.title = expanded ? '收起标题' : '展开标题';
        if (expanded) expandedDocOutlines.add(docId);
        else expandedDocOutlines.delete(docId);
        return;
      }

      const button = event.target.closest('[data-doc-id]');
      if (!button) return;
      const headingId = button.dataset.headingId || '';
      const hash = headingId ? `${button.dataset.docId}:${headingId}` : button.dataset.docId;
      if (decodeURIComponent(location.hash.replace(/^#/, '')) === hash) {
        renderDocument(findDoc(button.dataset.docId), headingId);
      } else {
        location.hash = hash;
      }
      document.body.classList.remove('nav-open');
    });

    searchInput.addEventListener('input', renderNav);

    openNavButton.addEventListener('click', () => {
      document.body.classList.add('nav-open');
    });

    document.addEventListener('keydown', event => {
      if (event.key === 'Escape') document.body.classList.remove('nav-open');
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        searchInput.focus();
      }
    });

    window.addEventListener('hashchange', () => {
      const { docId, headingId } = parseHash();
      renderDocument(findDoc(docId), headingId || '');
    });

    const initial = parseHash();
    renderDocument(findDoc(initial.docId), initial.headingId || '');
  </script>
</body>
</html>
'@

$html = $htmlTemplate.Replace('__DOCS_JSON__', $docsJson)
$utf8Bom = New-Object -TypeName System.Text.UTF8Encoding -ArgumentList $true
[System.IO.File]::WriteAllText($outputPath, $html, $utf8Bom)
Write-Host "Generated $outputPath from $($docs.Count) markdown documents."









