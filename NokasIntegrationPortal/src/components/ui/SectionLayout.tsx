import React from "react";

export default function SectionLayout({
  id,
  title,
  subtitle,
  children,
}: {
  id?: string;
  title: string;
  subtitle?: string;
  children: React.ReactNode;
}) {
  return (
    <section id={id} className="mb-16">
      <h2 className="text-2xl font-bold text-[#014737] mb-2">{title}</h2>
      {subtitle && <p className="text-sm text-gray-600 mb-4">{subtitle}</p>}
      <div>{children}</div>
    </section>
  );
}
