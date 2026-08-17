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

  function renderPptx(buf) {
    if (typeof FileReaderJS === "undefined") {
      return Promise.reject(new Error("缺少 filereader.js（FileReaderJS），无法渲染 PPTX。"));
    }
    if (typeof jQuery === "undefined" || typeof jQuery.fn.pptxToHtml !== "function") {
      return Promise.reject(new Error("PPTXjs 未正确加载。"));
    }

    // 同源 blob URL，避免跨域 XHR；PPTXjs 内部仍走 JSZipUtils.getBinaryContent
    var blob = new Blob([buf], {
      type: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    });
    var url = URL.createObjectURL(blob);

    container.innerHTML = "";
    var holder = document.createElement("div");
    holder.id = "pptx-host-" + Date.now();
    holder.className = "pptx";
    container.appendChild(holder);

    setStatus("正在渲染幻灯片…", false);

    return new Promise(function (resolve, reject) {
      var settled = false;
      var timer = setTimeout(function () {
        if (settled) return;
        settled = true;
        URL.revokeObjectURL(url);
        reject(new Error("PPTX 渲染超时，请改用结构化预览。"));
      }, 90000);

      try {
        jQuery(holder).pptxToHtml({
          pptxFileUrl: url,
          slidesScale: 0.7,
          slideMode: false,
          keyBoardShortCut: false,
          mediaProcess: false,
        });
      } catch (err) {
        clearTimeout(timer);
        URL.revokeObjectURL(url);
        settled = true;
        reject(err);
        return;
      }

      // pptxToHtml 异步写 DOM；轮询检测幻灯片或错误提示
      var tries = 0;
      var poll = setInterval(function () {
        tries++;
        var slides = holder.querySelectorAll(".slide");
        var loading = holder.querySelector(".slides-loadnig-msg, .slides-loading-msg");
        if (slides.length > 0) {
          clearInterval(poll);
          clearTimeout(timer);
          if (!settled) {
            settled = true;
            URL.revokeObjectURL(url);
            showContainer("pptx");
            resolve();
          }
          return;
        }
        // 长时间仍无 slide 且 loading 已消失 → 失败
        if (tries > 40 && (!loading || loading.style.display === "none") && !holder.innerHTML.trim()) {
          clearInterval(poll);
          clearTimeout(timer);
          if (!settled) {
            settled = true;
            URL.revokeObjectURL(url);
            reject(new Error("PPTX 未能渲染出任何幻灯片"));
          }
        }
      }, 250);
    });
  }

  var renderer = { docx: renderDocx, xlsx: renderXlsx, xls: renderXlsx, pptx: renderPptx }[kind];
  if (!renderer) {
    reportError("不支持的文档类型：" + kind);
    return;
  }

  setStatus("正在解析文档…", false);
  fetchBytes()
    .then(renderer)
    .catch(function (err) { reportError(err && err.message ? err.message : err); });
})();
