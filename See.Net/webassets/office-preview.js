// Office 网页预览：从 ?kind= 参数取得文档类型，经独立未映射域拉取文件字节流
// （宿主 WebResourceRequested 拦截回流），交给对应渲染库输出。
(function () {
  "use strict";

  // 与 OfficeWebHost.DataHost 一致；不可使用映射域上的相对 /data（WebResourceRequested 不触发）
  var DATA_URL = "https://see-office-data.local/data";

  var params = new URLSearchParams(location.search);
  var kind = params.get("kind") || "";
  var statusEl = document.getElementById("status");
  var container = document.getElementById("container");

  function setStatus(text, isError) {
    statusEl.className = isError ? "error" : "";
    statusEl.innerHTML = "";
    if (!isError) {
      var spin = document.createElement("div");
      spin.className = "spinner";
      statusEl.appendChild(spin);
    }
    statusEl.appendChild(document.createTextNode(text));
    statusEl.style.display = "flex";
    container.className = "hidden";
  }

  function showContainer(cls) {
    statusEl.style.display = "none";
    container.className = cls;
  }

  // 渲染失败上报宿主，宿主降级回结构化视图。
  function reportError(message) {
    try {
      window.chrome.webview.postMessage({ type: "error", kind: kind, message: String(message) });
    } catch (_) { /* 宿主可能已释放 */ }
    setStatus("渲染失败：" + message, true);
  }

  function fetchBytes() {
    return fetch(DATA_URL, { cache: "no-store" }).then(function (res) {
      if (!res.ok) throw new Error("拉取文件失败（HTTP " + res.status + "）");
      return res.arrayBuffer();
    });
  }

  function renderDocx(buf) {
    return mammoth.convertToHtml({ arrayBuffer: buf }).then(function (result) {
      container.innerHTML = result.value;
      // mammoth 的警告（如丢失图片）仅控制台提示，不中断
      if (result.messages && result.messages.length) {
        console.warn("mammoth warnings:", result.messages);
      }
      showContainer("docx");
    });
  }

  function renderXlsx(buf) {
    var data = new Uint8Array(buf);
    var wb = XLSX.read(data, { type: "array" });
    container.innerHTML = "";
    wb.SheetNames.forEach(function (name) {
      var heading = document.createElement("h4");
      heading.textContent = name;
      heading.style.margin = "16px 12px 4px";
      container.appendChild(heading);

      var holder = document.createElement("div");
      holder.innerHTML = XLSX.utils.sheet_to_html(wb.Sheets[name], { editable: false });
      container.appendChild(holder);
    });
    showContainer("xlsx");
    return Promise.resolve();
  }

  function renderPptx() {
    // PPTXjs 1.21.1 入口是 pptxToHtml()，通过 JSZipUtils.getBinaryContent(pptxFileUrl)
    // 以 XHR 拉取二进制 —— 指向未映射数据域。
    container.innerHTML = "";
    var holder = document.createElement("div");
    holder.className = "pptx";
    container.appendChild(holder);
    showContainer("pptx");

    var promise = jQuery(holder).pptxToHtml({
      pptxFileUrl: DATA_URL,
      slidesScale: 0.7,
      slideMode: false,
      keyBoardShortCut: false
    });
    // pptxToHtml 返回 jQuery 对象而非 Promise；成功与否以容器是否产出幻灯片判断
    return Promise.resolve(promise).then(function () {
      if (!holder.querySelector(".slide") && !holder.innerHTML.trim()) {
        throw new Error("PPTX 未能渲染出任何幻灯片");
      }
    });
  }

  var renderer = { docx: renderDocx, xlsx: renderXlsx, xls: renderXlsx, pptx: renderPptx }[kind];
  if (!renderer) {
    reportError("不支持的文档类型：" + kind);
    return;
  }

  setStatus("正在解析文档…", false);
  var work = kind === "pptx"
    ? renderPptx()                 // pptx 由 JSZipUtils 自行拉取数据域
    : fetchBytes().then(renderer);
  work.catch(function (err) { reportError(err && err.message ? err.message : err); });
})();
