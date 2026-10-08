/** CSS string literal for a `content:` value. */
const cssString = (s: string) =>
  `"${s.replace(/[\\"]/g, "\\$&").replace(/[\r\n]+/g, " ")}"`;

/** Running header and footer on every printed page: clinic, patient and
 * MRN on top, document title top right, "Page n of N" and a
 * confidentiality line at the bottom. Uses @page margin boxes (Chrome and
 * Edge print them; other browsers ignore them and keep the in-page
 * letterhead and footer). */
export function PageMargins({
  clinic,
  patient,
  mrn,
  title,
}: {
  clinic: string;
  patient: string;
  mrn: string;
  title: string;
}) {
  const box = "font-family: Arial, sans-serif; font-size: 9pt; color: #555;";
  const css = `@page {
  size: letter;
  margin: 18mm 14mm 16mm;
  @top-left { content: ${cssString(`${clinic} · ${patient} · MRN ${mrn}`)}; ${box} }
  @top-right { content: ${cssString(title)}; ${box} }
  @bottom-left { content: "Confidential patient information"; ${box} }
  @bottom-right { content: "Page " counter(page) " of " counter(pages); ${box} }
}`;
  return <style>{css}</style>;
}
