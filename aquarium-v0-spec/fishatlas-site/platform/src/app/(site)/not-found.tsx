export default function NotFound() {
  return (
    <div className="page">
      <h1>没有找到这个条目</h1>
      <p style={{ marginTop: 20 }}>This entry could not be found.</p>
      <a className="button" style={{ marginTop: 24 }} href="/zh-CN/aquatic">
        返回水族库 / Browse the library
      </a>
    </div>
  );
}
