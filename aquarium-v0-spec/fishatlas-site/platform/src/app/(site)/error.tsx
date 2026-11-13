"use client";
export default function SiteError({
  reset,
}: {
  error: Error;
  reset: () => void;
}) {
  return (
    <div className="page">
      <a className="brand" href="/zh-CN">
        FishAtlas
      </a>
      <h1 style={{ marginTop: 40 }}>资料暂时无法加载</h1>
      <p style={{ marginTop: 20 }}>请重试，或返回首页继续浏览。</p>
      <p>Content could not load. Retry or return home to browse.</p>
      <button style={{ marginTop: 24 }} onClick={reset}>
        重试 / Retry
      </button>
      <a style={{ marginLeft: 24 }} href="/zh-CN">
        返回首页 / Home
      </a>
    </div>
  );
}
