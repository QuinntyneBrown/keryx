/**
 * Hands a file to the browser to save under `name`: through an object URL and a link with the
 * `download` attribute, clicked once and removed. The URL is revoked as soon as the download has
 * started, so the file is not held in memory after (L2-054 AC6).
 */
export function saveFile(document: Document, file: Blob, name: string): void {
  const url = URL.createObjectURL(file);
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.hidden = true;
  document.body.append(link);
  try {
    link.click();
  } finally {
    link.remove();
    URL.revokeObjectURL(url);
  }
}
