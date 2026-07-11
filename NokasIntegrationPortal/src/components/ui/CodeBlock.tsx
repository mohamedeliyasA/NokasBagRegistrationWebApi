

export default function CodeBlock({
  code,
  language,
}: {
  code: string;
  language: string;
}) {
  return (
    <pre className="bg-gray-100 text-gray-800 p-4 rounded overflow-x-auto text-sm border">
      <code className={`language-${language}`}>{code}</code>
    </pre>
  );
}
