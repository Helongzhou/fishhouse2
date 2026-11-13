import type { ReactNode } from "react";
import "../../styles/site.css";
export default function SiteLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="zh-CN">
      <body>{children}</body>
    </html>
  );
}
