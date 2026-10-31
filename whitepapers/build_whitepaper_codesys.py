"""Erzeugt das Comquent-Whitepaper „SBOM für CODESYS-Projekte“ als PDF.

Aufruf:  python3 whitepapers/build_whitepaper_codesys.py
Ausgabe: whitepapers/Comquent_Whitepaper_SBOM_CODESYS.pdf

Layout folgt der Comquent-SBOM-Reihe („SBOM für SPS-Projekte mit TIA Portal V21“)
und den ReportLab-Bausteinen aus „KI in der SPS-Entwicklung“ (A4, Helvetica).
Quellen werden über cite() in der Reihenfolge ihrer ersten Nennung nummeriert;
das Literaturverzeichnis entsteht daraus automatisch. Die Seitenzahl „von N“
entsteht in einem zweiten Durchlauf.
"""

import io
import re
from pathlib import Path

from reportlab.graphics.shapes import Drawing, Line, Polygon, Rect, String
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    CondPageBreak,
    Frame,
    Image,
    KeepTogether,
    NextPageTemplate,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)

HERE = Path(__file__).resolve().parent
LOGO = HERE / "assets" / "comquent-logo.png"
COVER_IMAGE = HERE / "assets" / "blog-sbom-maschine-schaltschrank.jpg"
OUTPUT = HERE / "Comquent_Whitepaper_SBOM_CODESYS.pdf"

TITLE = "SBOM für CODESYS-Projekte"
RUNNING_TITLE = f"Whitepaper · {TITLE}"
STAND = "31. Oktober 2026"
ABRUF = "31.10.2026"

NAVY = colors.HexColor("#0e2a4a")
ORANGE = colors.HexColor("#e8520a")
ORANGE_BG = colors.HexColor("#fdf1ea")
TEXT = colors.HexColor("#222a33")
MUTED = colors.HexColor("#5a6b7d")
ROW_ALT = colors.HexColor("#f2f5f8")
BOX_BG = colors.HexColor("#eef2f6")
RULE = colors.HexColor("#c9d2dc")

# Schriften: Arial und Courier New einbetten, damit jeder Viewer gleich darstellt;
# ohne diese Dateien bleibt es bei den PDF-Standardschriften.
_FONT_DIR = Path("/System/Library/Fonts/Supplemental")
FONT, FONT_B, MONO = "Helvetica", "Helvetica-Bold", "Courier"
if (_FONT_DIR / "Arial.ttf").exists():
    pdfmetrics.registerFont(TTFont("Arial", str(_FONT_DIR / "Arial.ttf")))
    pdfmetrics.registerFont(TTFont("Arial-Bold", str(_FONT_DIR / "Arial Bold.ttf")))
    pdfmetrics.registerFont(TTFont("Arial-Italic", str(_FONT_DIR / "Arial Italic.ttf")))
    pdfmetrics.registerFont(TTFont("Arial-BoldItalic", str(_FONT_DIR / "Arial Bold Italic.ttf")))
    pdfmetrics.registerFontFamily("Arial", normal="Arial", bold="Arial-Bold",
                                  italic="Arial-Italic", boldItalic="Arial-BoldItalic")
    FONT, FONT_B = "Arial", "Arial-Bold"
if (_FONT_DIR / "Courier New.ttf").exists():
    pdfmetrics.registerFont(TTFont("CourierNew", str(_FONT_DIR / "Courier New.ttf")))
    MONO = "CourierNew"

PAGE_W, PAGE_H = A4
MARGIN_X = 22 * mm
CONTENT_W = PAGE_W - 2 * MARGIN_X
LOGO_RATIO = 1607 / 376

# ---------------------------------------------------------------- Quellen

REFS = {
    "cra": ("Europäische Union: Verordnung (EU) 2024/2847 (Cyber Resilience Act), Amtsblatt der EU, insbesondere "
            "Art. 3 Nr. 39, Art. 13 Abs. 13 und Anhang I Teil II Nr. 1.",
            "https://eur-lex.europa.eu/eli/reg/2024/2847/oj"),
    "tr": ("BSI: Technische Richtlinie TR-03183-2, Cyber Resilience Requirements for Manufacturers and Products, "
           "Part 2: Software Bill of Materials (SBOM), Version 2.1.0, 20.08.2025.",
           "https://www.bsi.bund.de/SharedDocs/Downloads/EN/BSI/Publications/TechGuidelines/TR03183/BSI-TR-03183-2_v2_1_0.pdf"),
    "taxo": ("BSI: TR-03183 CycloneDX Property Taxonomy, GitHub.",
             "https://github.com/BSI-Bund/tr-03183-cyclonedx-property-taxonomy"),
    "cdx": ("OWASP Foundation: CycloneDX v1.6 JSON Reference.",
            "https://cyclonedx.org/docs/1.6/json/"),
    "man23": ("3S-Smart Software Solutions GmbH: User Manual for PLC Programming with CoDeSys 2.3, Dokumentversion 6.0, "
              "CoDeSys V2.3.9.24, Stand 28.10.2010. Kapitel 2.1 (Project Components), 4.3 (Project Export), "
              "6.4 (Library Manager), 6.6 (PLC Configuration), 6.12 (Target Settings), Anhang F (Command Line).",
              "https://www.ee.pw.edu.pl/~purap/PLC/manuals/m07590333_00000000_1en.pdf"),
    "disc": ("CODESYS Group: CODESYS-Produkte in der Servicephase und abgekündigte Produkte, Eintrag „CODESYS V2.3, "
             "incl. CODESYS Control V2.3“. Angabe laut Suchindex; die Seite war am 31.10.2026 für automatisierte "
             "Abrufe gesperrt.",
             "https://de.codesys.com/ecosystem/up-to-date/release-lifecycle/abkuendigungen/"),
    "exp": ("CODESYS GmbH: Online-Hilfe CODESYS Development System, „Exporting and Importing Projects“.",
            "https://content.helpme-codesys.com/en/CODESYS%20Development%20System/_cds_project_export_import.html"),
    "libman": ("CODESYS GmbH: Online-Hilfe CODESYS Development System, „Object: Library Manager“.",
               "https://content.helpme-codesys.com/en/CODESYS%20Development%20System/_cds_obj_library_manager.html"),
    "robin": ("INESC TEC: Projekt „robin“, PLCopen-XML-Export eines CODESYS-V3.5-SP15-Projekts "
              "(robin_updater/cfg/codesys_project.xml), Apache-2.0, Commit 42ba562.",
              "https://github.com/ScalABLE40/robin"),
    "script": ("CODESYS GmbH: Online-Hilfe CODESYS Scripting Engine.",
               "https://content.helpme-codesys.com/en/ScriptingEngine/"),
    "c4help": ("CODESYS GmbH: Online-Hilfe CODESYS 4, Startseite.",
               "https://content.helpme-codesys.com/en/CODESYS%204/_c4_start_page.html"),
    "c4switch": ("CODESYS GmbH: Online-Hilfe CODESYS 4, „Switching from CODESYS 3“.",
                 "https://content.helpme-codesys.com/en/CODESYS%204/_c4_start_page-2083130.html"),
    "c4news": ("CODESYS Group: „CODESYS 4 Version 1.0.0.0 is available“, Meldung, September 2026. Abgerufen "
               "31.10.2026 im Rahmen der Comquent-Recherche; bei der Schlussprüfung für automatisierte Abrufe gesperrt.",
               "https://www.codesys.com/company/news-center/latest-news/codesys-4-version-1000-is-available/"),
    "product": ("Comquent GmbH: PLC SBOM Builder, Produktseite.",
                "https://comquent.de/produkte/plc-sbom-builder"),
    "tiawp": ("Comquent GmbH: Whitepaper „SBOM für SPS-Projekte mit TIA Portal V21“, Oktober 2026.",
              "https://comquent.de/ressourcen/whitepapers"),
}

_ref_order = []


def cite(*keys):
    """Gibt „[n]“ bzw. „[n, m]“ zurück und vergibt Nummern in Reihenfolge der ersten Nennung."""
    nums = []
    for k in keys:
        if k not in REFS:
            raise KeyError(f"Unbekannte Quelle: {k}")
        if k not in _ref_order:
            _ref_order.append(k)
        nums.append(_ref_order.index(k) + 1)
    return "[" + ", ".join(str(n) for n in nums) + "]"


# ---------------------------------------------------------------- Stile

def style(name, **kw):
    base = dict(fontName=FONT, fontSize=9.6, leading=14.6, textColor=TEXT, bulletFontName=FONT)
    base.update(kw)
    return ParagraphStyle(name, **base)


S_BODY = style("body", alignment=TA_LEFT, spaceAfter=7)
S_H1 = style("h1", fontName=FONT_B, fontSize=16.5, leading=21, textColor=NAVY)
S_H2 = style("h2", fontName=FONT_B, fontSize=11.6, leading=15, textColor=ORANGE,
             spaceBefore=9, spaceAfter=4, keepWithNext=1)
S_BULLET = style("bullet", alignment=TA_LEFT, leftIndent=12, bulletIndent=0, spaceAfter=3)
S_CAPTION = style("caption", fontSize=7.6, leading=10.2, textColor=MUTED, spaceBefore=4, spaceAfter=10)
S_CELL = style("cell", fontSize=8.1, leading=11)
S_CELL_B = style("cellb", fontSize=8.1, leading=11, fontName=FONT_B, textColor=NAVY)
S_HEAD = style("head", fontSize=8.3, leading=11, fontName=FONT_B, textColor=colors.white)
S_BOX_T = style("boxt", fontName=FONT_B, fontSize=10.2, leading=14, textColor=ORANGE, spaceAfter=5)
S_BOX_T_NAVY = style("boxtn", fontName=FONT_B, fontSize=9.6, leading=13, textColor=NAVY, spaceAfter=3)
S_BOX = style("box", fontSize=9, leading=13.4, textColor=NAVY, spaceAfter=3)
S_KEY = style("key", fontSize=8.9, leading=13, textColor=TEXT, leftIndent=14, bulletIndent=0,
              spaceAfter=4, bulletFontName="ZapfDingbats", bulletFontSize=6, bulletColor=ORANGE)
S_REF = style("ref", fontSize=7.6, leading=10.4, leftIndent=22, firstLineIndent=-22, spaceAfter=3.5, splitLongWords=1)
S_SMALL = style("small", fontSize=7.6, leading=10.6, textColor=MUTED)
S_TOC = style("toc", fontSize=9.6, leading=13, textColor=NAVY)
S_CTA_T = style("ctat", fontName=FONT_B, fontSize=12, leading=16, textColor=colors.white, spaceAfter=5)
S_CTA = style("cta", fontSize=9, leading=13.4, textColor=colors.white, spaceAfter=3)

# Cover
S_KICKER = style("kicker", fontName=FONT_B, fontSize=8.6, leading=11, textColor=ORANGE, spaceAfter=10)
S_TITLE = style("title", fontName=FONT_B, fontSize=25, leading=31, textColor=NAVY, spaceAfter=10)
S_SUB = style("sub", fontSize=11, leading=15.5, textColor=MUTED, spaceAfter=10)
S_CO = style("co", fontName=FONT_B, fontSize=9.4, leading=13, textColor=NAVY)
S_CO_S = style("cos", fontSize=8, leading=11.5, textColor=MUTED)

_AMP = re.compile(r"&(?!amp;|lt;|gt;|quot;|nbsp;|#\d+;)")


def P(text, st=S_BODY):
    return Paragraph(_AMP.sub("&amp;", text), st)


def C(text):
    """Code-Schrift für Dateinamen und Bezeichner."""
    return f"<font name='{MONO}' size='8.4'>{text}</font>"


CHAPTERS = []


def H1(text, new_page=True):
    CHAPTERS.append(text)
    num = len(CHAPTERS)
    t = Table([[P(f"{num}&nbsp;&nbsp;{text}", S_H1)]], colWidths=[CONTENT_W])
    t.setStyle(TableStyle([
        ("LINEBELOW", (0, 0), (-1, -1), 1.1, ORANGE),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("RIGHTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    t.keepWithNext = True
    sp = Spacer(1, 10)
    sp.keepWithNext = True
    return ([PageBreak()] if new_page else [CondPageBreak(70 * mm)]) + [t, sp]


def H2(text):
    return [CondPageBreak(40 * mm), P(text, S_H2)]


def bullets(items, st=S_BULLET):
    return [Paragraph(_AMP.sub("&amp;", t), st, bulletText="–") for t in items]


def _framed(content, bg, bar, pad=12):
    t = Table([[content]], colWidths=[CONTENT_W])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), bg),
        ("LINEBEFORE", (0, 0), (0, -1), 3.2, bar),
        ("LEFTPADDING", (0, 0), (-1, -1), pad),
        ("RIGHTPADDING", (0, 0), (-1, -1), pad),
        ("TOPPADDING", (0, 0), (-1, -1), 10),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 10),
    ]))
    return [Spacer(1, 4), t, Spacer(1, 10)]


def key_box(title, items):
    content = [P(title, S_BOX_T)]
    content += [Paragraph(_AMP.sub("&amp;", t), S_KEY, bulletText="n") for t in items]
    return _framed(content, ORANGE_BG, ORANGE)


def info_box(title, paras):
    return _framed([P(title, S_BOX_T_NAVY)] + [P(t, S_BOX) for t in paras], BOX_BG, NAVY)


def cta_box(title, paras):
    content = [P(title, S_CTA_T)] + [P(t, S_CTA) for t in paras]
    t = Table([[content]], colWidths=[CONTENT_W])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), NAVY),
        ("LEFTPADDING", (0, 0), (-1, -1), 16),
        ("RIGHTPADDING", (0, 0), (-1, -1), 16),
        ("TOPPADDING", (0, 0), (-1, -1), 14),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 14),
    ]))
    return [Spacer(1, 8), KeepTogether([t]), Spacer(1, 10)]


def table(header, rows, widths, first_col_bold=True):
    data = [[P(h, S_HEAD) for h in header]]
    for r in rows:
        data.append([P(c, S_CELL_B if (i == 0 and first_col_bold) else S_CELL) for i, c in enumerate(r)])
    w = [CONTENT_W * f for f in widths]
    t = Table(data, colWidths=w, repeatRows=1)
    st = [
        ("BACKGROUND", (0, 0), (-1, 0), NAVY),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 6),
        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LINEBELOW", (0, 1), (-1, -1), 0.4, RULE),
    ]
    for i in range(1, len(data)):
        if i % 2 == 0:
            st.append(("BACKGROUND", (0, i), (-1, i), ROW_ALT))
    t.setStyle(TableStyle(st))
    return [Spacer(1, 3), t, Spacer(1, 9)]


_fig = [0]


def caption(text):
    _fig[0] += 1
    return P(f"<b>Abbildung {_fig[0]}:</b> {text}", S_CAPTION)


def figure(drawing, text):
    return [KeepTogether([Spacer(1, 4), drawing, caption(text)])]


# ---------------------------------------------------------------- Grafiken

def _arrow_down(d, x, y, col=NAVY):
    d.add(Polygon([x, y, x - 3, y + 5, x + 3, y + 5], fillColor=col, strokeColor=None))


def diagram_station():
    """Maschine > Station > Anwenderprogramm > Bibliotheken, Laufzeit, Firmware, Feldbusgeräte."""
    W, H = CONTENT_W, 236
    d = Drawing(W, H)
    cx = W / 2
    # Maschine
    mw, mh = 176, 36
    my = H - 4 - mh
    d.add(Rect(cx - mw / 2, my, mw, mh, fillColor=NAVY, strokeColor=None, rx=4, ry=4))
    d.add(String(cx, my + 21, "Maschine", fontName=FONT_B, fontSize=8.8,
                 fillColor=colors.white, textAnchor="middle"))
    d.add(String(cx, my + 9, "eine SBOM je Release", fontName=FONT, fontSize=7.2,
                 fillColor=colors.white, textAnchor="middle"))
    # Station (Container)
    st_top = my - 22
    st_bottom = 4
    d.add(Line(cx, my, cx, st_top + 5, strokeColor=NAVY, strokeWidth=0.9))
    _arrow_down(d, cx, st_top)
    d.add(Rect(0, st_bottom, W, st_top - st_bottom, fillColor=None, strokeColor=RULE,
               strokeWidth=0.9, strokeDashArray=[3, 3], rx=4, ry=4))
    d.add(String(10, st_top - 15, "Station: eine CODESYS-Steuerung", fontName=FONT_B,
                 fontSize=8.2, fillColor=NAVY))
    # Anwenderprogramm
    pw, ph = 196, 36
    py = st_top - 30 - ph
    d.add(Rect(cx - pw / 2, py, pw, ph, fillColor=colors.white, strokeColor=NAVY, strokeWidth=0.9, rx=4, ry=4))
    d.add(String(cx, py + 21, "Anwenderprogramm", fontName=FONT_B, fontSize=8.4,
                 fillColor=NAVY, textAnchor="middle"))
    d.add(String(cx, py + 9, "Version = Release + Git-Commit", fontName=FONT, fontSize=7.2,
                 fillColor=MUTED, textAnchor="middle"))
    # Komponenten
    boxes = [
        ("Bibliotheken", "Version im Projekt", "V2.3: nur Zeitstempel", False),
        ("Laufzeit", "CODESYS Control", "Stand vom Hersteller", True),
        ("Firmware Steuerung", "nicht im Projekt", "Ist-Stand aus dem Feld", True),
        ("Feldbusgeräte", "Firmware nicht", "im Projekt", True),
    ]
    pad, gap = 12, 10
    bw = (W - 2 * pad - 3 * gap) / 4
    bh = 54
    by = st_bottom + 12
    mid_y = (py + by + bh) / 2
    d.add(Line(cx, py, cx, mid_y, strokeColor=NAVY, strokeWidth=0.9))
    d.add(String(cx + 6, mid_y + 4, "hängt ab von", fontName=FONT, fontSize=6.8, fillColor=MUTED))
    centers = [pad + i * (bw + gap) + bw / 2 for i in range(4)]
    d.add(Line(centers[0], mid_y, centers[-1], mid_y, strokeColor=NAVY, strokeWidth=0.9))
    for i, (name, l1, l2, gap_flag) in enumerate(boxes):
        x = pad + i * (bw + gap)
        bxc = centers[i]
        d.add(Line(bxc, mid_y, bxc, by + bh + 5, strokeColor=NAVY, strokeWidth=0.9))
        _arrow_down(d, bxc, by + bh)
        d.add(Rect(x, by, bw, bh, fillColor=ORANGE_BG if gap_flag else colors.white,
                   strokeColor=ORANGE if gap_flag else NAVY, strokeWidth=1.3 if gap_flag else 0.9, rx=4, ry=4))
        if gap_flag:
            d.add(Rect(x, by + bh - 3.5, bw, 3.5, fillColor=ORANGE, strokeColor=None))
        d.add(String(bxc, by + bh - 17, name, fontName=FONT_B, fontSize=7.9,
                     fillColor=NAVY, textAnchor="middle"))
        d.add(String(bxc, by + bh - 31, l1, fontName=FONT, fontSize=7, fillColor=TEXT, textAnchor="middle"))
        d.add(String(bxc, by + bh - 42, l2, fontName=FONT, fontSize=7, fillColor=TEXT, textAnchor="middle"))
    return d


def diagram_pipeline():
    steps = [
        ("Quelle bereitstellen", ".exp, PLCopen XML", "oder C4-Projekt", False),
        ("Auslesen", "je Station: Geräte,", "Bibliotheken, Werkzeug", False),
        ("Zusammenführen", "Hersteller- und", "Teil-SBOMs zuordnen", False),
        ("Prüfen", "CycloneDX 1.6 und", "BSI TR-03183-2", True),
        ("Ablegen", "Archiv, optional", "Dependency-Track", False),
    ]
    W, H = CONTENT_W, 118
    d = Drawing(W, H)
    n = len(steps)
    gap = 11
    bw = (W - gap * (n - 1)) / n
    bh = 54
    y = H - 16 - bh
    for i, (t, s1, s2, gate) in enumerate(steps):
        x = i * (bw + gap)
        d.add(Rect(x, y, bw, bh, fillColor=colors.white if gate else ROW_ALT, strokeColor=NAVY, strokeWidth=0.8))
        if gate:
            d.add(Rect(x, y + bh - 3.5, bw, 3.5, fillColor=ORANGE, strokeColor=None))
        d.add(String(x + bw / 2, y + bh - 17, t, fontName=FONT_B, fontSize=7.8,
                     fillColor=NAVY, textAnchor="middle"))
        d.add(String(x + bw / 2, y + bh - 31, s1, fontName=FONT, fontSize=7, fillColor=MUTED, textAnchor="middle"))
        d.add(String(x + bw / 2, y + bh - 41, s2, fontName=FONT, fontSize=7, fillColor=MUTED, textAnchor="middle"))
        if i < n - 1:
            ax, ay = x + bw + 1.5, y + bh / 2
            d.add(Line(ax, ay, ax + gap - 4, ay, strokeColor=NAVY, strokeWidth=0.9))
            d.add(Polygon([ax + gap - 3, ay, ax + gap - 7, ay + 2.6, ax + gap - 7, ay - 2.6],
                          fillColor=NAVY, strokeColor=None))
    gx = 3 * (bw + gap) + bw / 2
    fy = y - 20
    d.add(Line(gx, y, gx, fy, strokeColor=ORANGE, strokeWidth=1))
    d.add(String(gx, fy - 10, "Release-Build: fehlende Pflichtangabe bricht den Build ab",
                 fontName=FONT, fontSize=7.2, fillColor=ORANGE, textAnchor="middle"))
    d.add(String(W, H - 8, "Orange Kante = Quality Gate", fontName=FONT, fontSize=6.8,
                 fillColor=MUTED, textAnchor="end"))
    return d


# ---------------------------------------------------------------- Seitenlayout

TOTAL_PAGES = [0]


def draw_cover(c, doc):
    c.saveState()
    c.setFillColor(ORANGE)
    c.rect(0, PAGE_H - 6, PAGE_W, 6, stroke=0, fill=1)
    c.setFillColor(NAVY)
    c.rect(0, 0, PAGE_W, 5, stroke=0, fill=1)
    c.restoreState()


def draw_body(c, doc):
    c.saveState()
    lh = 15
    c.drawImage(str(LOGO), MARGIN_X, PAGE_H - 21 * mm, lh * LOGO_RATIO, lh, mask="auto")
    c.setFont(FONT, 7.8)
    c.setFillColor(MUTED)
    c.drawRightString(PAGE_W - MARGIN_X, PAGE_H - 21 * mm + 4, RUNNING_TITLE)
    c.setStrokeColor(RULE)
    c.setLineWidth(0.6)
    c.line(MARGIN_X, PAGE_H - 23.5 * mm, PAGE_W - MARGIN_X, PAGE_H - 23.5 * mm)
    c.line(MARGIN_X, 16.5 * mm, PAGE_W - MARGIN_X, 16.5 * mm)
    c.setFont(FONT, 7.4)
    c.drawString(MARGIN_X, 12 * mm, "Comquent GmbH · Lindberghstr. 7 · 82178 Puchheim · comquent.de")
    total = TOTAL_PAGES[0] or "?"
    c.drawRightString(PAGE_W - MARGIN_X, 12 * mm, f"Seite {doc.page} von {total}")
    c.restoreState()


# ---------------------------------------------------------------- Inhalt

def cover():
    s = []
    lh = 34
    s.append(Image(str(LOGO), width=lh * LOGO_RATIO, height=lh, hAlign="LEFT"))
    s.append(Spacer(1, 30))
    s.append(P("W H I T E P A P E R", S_KICKER))
    s.append(P("SBOM für CODESYS-Projekte<br/>V2.3, V3.5 und CODESYS 4", S_TITLE))
    s.append(P("Software Bill of Materials nach Cyber Resilience Act und BSI TR-03183-2: was sich aus "
               "den drei CODESYS-Generationen auslesen lässt und wo Lücken bleiben", S_SUB))
    rule = Table([[Spacer(1, 1)]], colWidths=[CONTENT_W], rowHeights=[2])
    rule.setStyle(TableStyle([("LINEBELOW", (0, 0), (-1, -1), 1.4, ORANGE)]))
    s += [rule, Spacer(1, 18)]
    from PIL import Image as PILImage
    w, h = PILImage.open(COVER_IMAGE).size
    s.append(Image(str(COVER_IMAGE), width=CONTENT_W, height=CONTENT_W * h / w))
    s.append(caption("Steuerungstechnik an der Maschine: Laufzeit, Firmware, Bibliotheken und Anwenderprogramm "
                     "gehören in die SBOM (KI-generierte Illustration)."))
    s.append(Spacer(1, 16))
    s.append(P("Comquent GmbH", S_CO))
    s.append(P("Industrial DevOps · CI/CD · DevSecOps · IT/OT-Konvergenz", S_CO_S))
    s.append(P("Stand: Oktober 2026 · Bezug: CODESYS V2.3, CODESYS V3.5 und CODESYS 4", S_CO_S))
    return s


def toc(chapters):
    s = [Spacer(1, 2)]
    t = Table([[P("Inhalt", S_H1)]], colWidths=[CONTENT_W])
    t.setStyle(TableStyle([
        ("LINEBELOW", (0, 0), (-1, -1), 1.1, ORANGE),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    s += [t, Spacer(1, 12)]
    rows = [[P(f"<font color='#e8520a'><b>{i:02d}</b></font>", S_TOC), P(name, S_TOC)]
            for i, name in enumerate(chapters, start=1)]
    tt = Table(rows, colWidths=[12 * mm, CONTENT_W - 12 * mm])
    tt.setStyle(TableStyle([
        ("LINEBELOW", (0, 0), (-1, -1), 0.4, RULE),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]))
    s += [tt, Spacer(1, 26)]
    s += info_box("Für wen dieses Whitepaper gedacht ist", [
        "Leiterinnen und Leiter der Steuerungstechnik, SPS-Programmierer sowie Produkt-Security- und "
        "Compliance-Verantwortliche im Maschinen- und Anlagenbau, die Steuerungen mit CODESYS oder einer "
        "darauf aufbauenden Hersteller-IDE projektieren und für ihre Maschinen eine SBOM nach dem Cyber "
        "Resilience Act aufbauen wollen. Besonders angesprochen sind Unternehmen, in deren Maschinenpark "
        "mehrere CODESYS-Generationen nebeneinander laufen.",
    ])
    return s


def build_body():
    s = []

    # 1 Einleitung -------------------------------------------------------
    s += H1("Einleitung", new_page=False)
    s.append(P(
        "Ab dem 11. Dezember 2027 braucht jede Maschine, die in der EU neu in Verkehr gebracht wird, eine "
        "Software Bill of Materials (SBOM). Der Cyber Resilience Act verlangt in Anhang I Teil II Nr. 1 eine "
        "SBOM in einem gängigen, maschinenlesbaren Format, die mindestens die obersten Abhängigkeiten des "
        f"Produkts abdeckt {cite('cra')}. Sie gehört zur technischen Dokumentation, muss nicht veröffentlicht "
        "werden und wird mindestens zehn Jahre aufbewahrt. Die Meldepflichten für aktiv ausgenutzte "
        "Schwachstellen gelten bereits seit dem 11. September 2026."))
    s.append(P(
        "Für eine Maschine mit CODESYS-Steuerung ist die Frage, wie diese SBOM entsteht, schwieriger als für "
        "ein IT-Projekt. Es gibt keine Paketmanifeste, die ein Generator wie Syft oder cdxgen auswerten "
        "könnte, und keine CODESYS-Generation exportiert eine SBOM. Dazu kommt eine Eigenheit des "
        "Maschinenbaus: Steuerungen laufen fünfzehn oder zwanzig Jahre. In vielen Maschinenparks stehen "
        "deshalb mehrere CODESYS-Generationen nebeneinander, die Bestandsbaureihe auf CODESYS V2.3, die "
        "aktuelle auf V3.5. Seit September 2026 gibt es mit CODESYS 4 ein drittes Werkzeug "
        f"{cite('c4news')}."))
    s.append(P(
        "Die drei Generationen speichern Projekte, Bibliotheken und Gerätebeschreibungen grundverschieden. "
        "Davon hängt ab, welche Angaben sich automatisch auslesen lassen, welche Lücken bleiben und wie viel "
        "Handarbeit jede Maschine kostet. Dieses Whitepaper geht die Generationen der Reihe nach durch, "
        "stellt sie nebeneinander und zeigt, wie die SBOM in jedem Build entsteht. Die Aussagen zu "
        "Projektformaten stützen sich auf die Handbücher und die Online-Hilfe von CODESYS sowie auf "
        "öffentlich verfügbare Exportdateien. Alle Quellen stehen im Referenzverzeichnis."))
    s.append(P(
        "Eine Vorbemerkung: Eine SBOM allein erfüllt den CRA nicht. Sie ist ein Baustein der technischen "
        "Dokumentation. Ihren Nutzen entfaltet sie erst, wenn sie laufend gegen neue Sicherheitshinweise "
        "der Hersteller geprüft wird."))

    # 2 Auf einen Blick --------------------------------------------------
    s += H1("Auf einen Blick")
    s += key_box("Kernaussagen", [
        "<b>Die SBOM ist Pflicht.</b> Ab dem 11. Dezember 2027 verlangt der CRA für jede neu in Verkehr "
        "gebrachte Maschine eine maschinenlesbare SBOM, auch für Steuerungen mit CODESYS.",
        "<b>Keine CODESYS-Generation exportiert eine SBOM.</b> Sie entsteht aus Projektexporten, "
        "Herstellerangaben und der Dokumentation des eigenen Codes.",
        "<b>V2.3 kennt keine Bibliotheksversionen.</b> Der Bibliotheksverwalter führt Dateiname und "
        "Zeitstempel. Belastbar wird die Angabe erst, wenn die Bibliotheksdatei im Repository liegt.",
        "<b>V3.5 löst Versionen erst beim Übersetzen auf.</b> Platzhalter und Verweise auf die neueste "
        "Version machen die SBOM vom Repository des Build-Rechners abhängig.",
        "<b>CODESYS 4 bringt eine Lock-Datei mit.</b> Sie nennt alle direkten und indirekten Bibliotheken "
        "mit exakter Version und Prüfsumme und ist die beste Quelle der drei Generationen.",
        "<b>Die Firmware steht in keinem Projekt.</b> Die Version einer Gerätebeschreibung ist nicht die "
        "Firmware des Geräts. Den Ist-Stand liefert die Inbetriebnahme.",
        "<b>Lücken gehören in die SBOM.</b> Was keine Quelle hergibt, wird vermerkt, nicht geschätzt. Nur "
        "so trägt der spätere Abgleich mit Sicherheitshinweisen.",
    ])

    # 3 Inhalt der SBOM --------------------------------------------------
    s += H1("Was in die SBOM einer CODESYS-Steuerung gehört")
    s.append(P(
        "Der CRA definiert die SBOM als Aufstellung der Softwarebestandteile eines Produkts "
        f"{cite('cra')}. Hardware ist damit kein Pflichtinhalt. Sie liefert aber den Zusammenhang, ohne den "
        "eine SBOM für eine Maschine wenig taugt: zu welchem Gerät welche Firmware gehört, an welcher "
        "Steuerung welches Programm läuft. Für eine CODESYS-Station sind es fünf Arten von Bausteinen."))
    s += table(
        ["Baustein", "Beispiel", "Woher die Angabe kommt"],
        [
            ["Laufzeit und Firmware", "CODESYS Control auf der Steuerung, Firmware eines Buskopplers",
             "Steuerungs- bzw. Gerätehersteller; im Projekt steht meist nur ein Sollwert oder gar nichts"],
            ["Bibliotheken", f"{C('standard.lib')} (V2.3), {C('SM3_Basic, 4.5.1.0')} (V3.5), Bibliotheken des "
             "Steuerungsherstellers", "Bibliotheksverwaltung des Projekts"],
            ["Anwenderprogramm", "eigener Steuerungscode der Maschine",
             "Quellen im Versionsverwaltungssystem, Version aus Release und Commit"],
            ["Feldbusgeräte mit Firmware", "CANopen-, PROFIBUS- oder EtherCAT-Teilnehmer",
             "Steuerungskonfiguration; die Firmware steht fast nie im Projekt"],
            ["Zugekaufte Software", "geschützte Bibliotheken Dritter, Web-Anteile einer Visualisierung",
             "Lieferant, Teil-SBOMs aus Syft oder cdxgen"],
        ],
        [0.22, 0.38, 0.40],
    )
    s += figure(diagram_station(),
                "Aufbau einer CODESYS-Station in der SBOM. Orange markiert sind Bestandteile, die in keinem "
                "CODESYS-Projekt stehen; ihre Angaben kommen aus einer Hersteller-SBOM oder aus dem Feld.")

    s += H2("3.1 Anforderungen der BSI TR-03183-2")
    s.append(P(
        "Für Format und Pflichtfelder hat sich die Technische Richtlinie TR-03183-2 des BSI durchgesetzt, "
        f"aktuell in Version 2.1.0 vom August 2025 {cite('tr')}. Sie ist nicht bindend, wird aber in "
        "Ausschreibungen und Lieferverträgen verlangt. Sie fordert CycloneDX ab Version 1.6 oder SPDX ab "
        "Version 3.0.1 und je Komponente unter anderem Ersteller, Name, Version, Dateiname, Abhängigkeiten, "
        "Distributionslizenz und einen SHA-512-Hash der ausgelieferten Form. Schwachstellen gehören "
        "ausdrücklich nicht in die SBOM. Für CycloneDX hat das BSI die nötigen Felder in einer eigenen "
        f"Property-Taxonomie festgelegt {cite('taxo', 'cdx')}."))

    s += H2("3.2 Gerätebeschreibung ist nicht Firmware")
    s.append(P(
        "Ein Punkt zieht sich durch alle drei CODESYS-Generationen, und er ist die häufigste Fehlerquelle "
        "bei selbst gebauten SBOMs: Die Version einer Gerätebeschreibung oder Zielsystemdatei ist nicht die "
        "Firmware-Version des Geräts. Ein öffentlich verfügbarer PLCopen-XML-Export eines CODESYS-V3.5-Projekts "
        f"zeigt das gut {cite('robin')}. Die Datei wurde mit CODESYS V3.5 SP15 geschrieben, die Steuerung "
        f"trägt die Kennung {C('Version 3.5.15.0')}. Die Zahl beschreibt die Gerätebeschreibung, die auf dem "
        "Engineering-Rechner installiert war. Welche Laufzeit oder Firmware auf der Steuerung im Feld läuft, "
        "sagt sie nicht."))
    s.append(P(
        "Wer diese Version als Firmware in die SBOM schreibt, liefert eine Angabe, die jede Formatprüfung "
        "besteht und trotzdem falsch ist. Beim Abgleich mit Sicherheitshinweisen der Hersteller führt sie "
        "entweder zu falschen Treffern oder, schlimmer, zu übersehenen."))

    # 4 V2.3 -------------------------------------------------------------
    s += H1("CODESYS V2.3: Bibliotheken mit Zeitstempel")
    s.append(P(
        "Bei V2.3 lässt sich eine SBOM nur über einen Textexport aufbauen, und Bibliotheken haben keine "
        "Versionsnummer, sondern einen Dateizeitstempel. Wer heute noch Maschinen mit V2.3 ausliefert, "
        "braucht deshalb den längsten Vorlauf."))
    s += H2("4.1 Stand der Linie")
    s.append(P(
        "Die Linie endet mit Version 2.3.9.x; eine Version 2.5 gibt es nicht. CODESYS führt V2.3 "
        "einschließlich CODESYS Control V2.3 seit dem 1. Juli 2023 in der Servicephase und nennt V3 als "
        f"Nachfolger {cite('disc')}. Viele Hersteller-IDEs bauen auf V2.3 auf, etwa WAGO-I/O-PRO. Für "
        "Bestandsmaschinen, die in unveränderter Bauart weiter verkauft werden, gilt die SBOM-Pflicht "
        "trotzdem: Maßgeblich ist das Inverkehrbringen, nicht das Alter der Konstruktion."))
    s += H2("4.2 Projekt und Zielsystem")
    s.append(P(
        f"Ein Projekt ist eine einzelne Datei {C('*.pro')}, deren Format nicht öffentlich beschrieben ist. "
        "Neben den Bausteinen enthält sie die Ressourcen, darunter laut Handbuch den Bibliotheksverwalter, "
        f"die Steuerungskonfiguration und die Zielsystemeinstellungen {cite('man23')}. Die Steuerung wählt "
        "man über ein Target Support Package (TSP). Es besteht aus einer binären Zieldatei "
        f"{C('*.trg')}, einer Info-Datei {C('*.tnf')} und weiteren Dateien wie Bibliotheken und Treibern. "
        "Das TSP beschreibt die Plattform, nicht den Firmware-Stand der Steuerung im Feld."))
    s += H2("4.3 Bibliotheken")
    s.append(P(
        f"Bibliotheken sind Dateien {C('*.lib')}, intern in CODESYS geschrieben oder extern in C "
        f"implementiert, dann mit einer Header-Datei {C('*.h')}. Der Bibliotheksverwalter führt jede "
        "Bibliothek mit Dateiname und „date and time of file“, im Handbuch etwa "
        f"{C('standard.lib 30.3.99 11:30:14')} {cite('man23')}. Eine Versionsnummer wie in V3 gibt es "
        "nicht. Weicht der Zeitstempel der gefundenen Datei vom gespeicherten ab, markiert CODESYS den "
        f"Eintrag mit {C('*')}."))
    s.append(P(
        "Für die SBOM ist der Zeitstempel damit die einzige Versionsangabe aus dem Projekt. Die TR-03183-2 "
        "lässt für Komponenten ohne Versionsnummer das Änderungsdatum als Version zu. Belastbar ist das "
        f"aber nur, wenn die {C('.lib')}-Datei selbst im Repository liegt und gehasht werden kann. "
        "Unsere Empfehlung: Legen Sie alle eingebundenen Bibliotheken mit dem Projekt ab. Das hilft nicht "
        "nur der SBOM, sondern auch jedem, der das Projekt in zehn Jahren noch einmal übersetzen muss."))
    s += H2("4.4 Steuerungskonfiguration")
    s.append(P(
        f"E/A und Feldbusgeräte beruhen auf Konfigurationsdateien {C('*.cfg')} und Gerätedateien wie "
        f"{C('*.gsd')} für PROFIBUS und {C('*.eds')} für CANopen. Der Dialog eines PROFIBUS-Teilnehmers zeigt "
        f"„HW Release and SW Release“ aus der GSD-Datei {cite('man23')}. Das ist der Stand, den die Gerätedatei "
        "beschreibt, nicht die Firmware im Gerät."))
    s.append(CondPageBreak(95 * mm))
    s += H2("4.5 Exportwege")
    s += table(
        ["Weg", "Ergebnis", "Bewertung für die SBOM"],
        [
            ["Projekt > Exportieren", f"ASCII-Datei {C('*.exp')}, wahlweise eine Datei je Objekt. Exportierbar "
             "sind Bausteine, Datentypen, Visualisierungen und Ressourcen sowie die Einträge des "
             "Bibliotheksverwalters, „not the libraries themselves“.",
             "Bevorzugter Weg: lesbar, versionierbar, ohne Reverse Engineering."],
            ["Batch-Modus", f"{C('codesys.exe /batch /cmd datei')} mit den Befehlen {C('file open')}, "
             f"{C('project export')} oder {C('project expmul')}.",
             "Erzeugt den Export ohne Oberfläche auf einem Windows-Agenten in der Pipeline."],
            ["PLCopen XML", "Im Handbuch V2.3.9.24 nicht beschrieben.",
             "Je Hersteller-IDE prüfen; nicht voraussetzen."],
            ["CODESYS V2.3 Converter", "Überführt das Projekt nach V3.5; Gerät und Bibliotheken werden neu "
             "zugeordnet.", "Beschreibt das umgewandelte Projekt, nicht die ausgelieferte V2.3-Maschine. "
             "Nur als Notlösung."],
        ],
        [0.2, 0.47, 0.33],
    )
    s.append(P(
        f"Offen ist, wie vollständig der {C('.exp')}-Export Zielsystem und Steuerungskonfiguration "
        "wiedergibt. Das Handbuch nennt die Ressourcen als exportierbar, beschreibt das Format aber nicht. "
        f"Das lässt sich nur an einem echten Projekt klären {cite('man23')}."))

    # 5 V3.5 -------------------------------------------------------------
    s += H1("CODESYS V3.5: Versionen nach der Auflösung")
    s.append(P(
        "V3.5 führt Bibliotheken mit Versionsnummer und Hersteller, und ein PLCopen-XML-Export liefert "
        "Gerät und Bibliotheken in lesbarer Form. Die Schwierigkeit liegt woanders: Welche "
        "Bibliotheksversion tatsächlich in die Steuerung geladen wird, entscheidet sich erst auf dem "
        "Rechner, auf dem übersetzt wird."))
    s += H2("5.1 Projekt und Geräte")
    s.append(P(
        f"Das Projekt ist eine binäre Datei {C('*.project')}. Sie enthält einen Gerätebaum mit der Steuerung, "
        "ihren Applikationen und den Feldbusgeräten. Jedes Gerät verweist auf eine Gerätebeschreibung "
        f"{C('*.devdesc.xml')}, die im Geräte-Repository des Rechners installiert sein muss. Die Kennung im "
        "Projekt besteht aus Typ, Id und Version; die Version gehört zur Gerätebeschreibung."))
    s += H2("5.2 Bibliotheken und Platzhalter")
    s.append(P(
        f"Bibliotheken liegen als {C('*.library')} oder {C('*.compiled-library')} in einem oder mehreren "
        "Bibliotheks-Repositories. Im Projekt stehen sie auf zwei Arten. Ein fester Verweis nennt Name, "
        f"Version und Hersteller, im öffentlichen Beispielexport etwa {C('Robin, 0.1.2 (INESC TEC)')} "
        f"{cite('robin')}. Ein Platzhalter wie {C('#SM3_Basic')} nennt dagegen nur eine Standardauflösung, "
        f"hier {C('SM3_Basic, 4.5.1.0 (3S - Smart Software Solutions GmbH)')}. Welche Bibliothek tatsächlich "
        "dahintersteht, legen die Gerätebeschreibung oder der Platzhalter-Dialog fest."))
    s.append(P(
        "Die Online-Hilfe nennt das Ergebnis „Effective version“ und stellt klar: „This version of the "
        f"library is used in the application“ {cite('libman')}. Für die SBOM folgt daraus eine einfache "
        "Regel. Die Version im Verweis ist ein Wunsch, die effektive Version ist die Wahrheit. Eine SBOM, die "
        "nur den Verweis liest, kann von der geladenen Software abweichen, sobald zwei Rechner "
        "unterschiedliche Repositories haben. Ist ein Verweis auf die jeweils neueste Version gesetzt, hängt "
        "das Ergebnis sogar davon ab, wann zuletzt jemand ein Bibliothekspaket installiert hat."))
    s += H2("5.3 Exportwege")
    s += table(
        ["Weg", "Ergebnis", "Bewertung für die SBOM"],
        [
            ["Projekt > Exportieren", f"CODESYS-XML {C('*.export')}, laut Hilfe „fully compatible with the "
             f"CODESYS project format“ {cite('exp')}.", "Vollständig, aber ein internes Format."],
            ["Projekt > PLCopen XML exportieren", "Austauschformat; nur eine Teilmenge, „100% compatibility "
             f"cannot be guaranteed“ {cite('exp')}. Enthält im Bereich {C('addData')} Gerätekennung und "
             f"Bibliotheksverweise, im Kopf die CODESYS-Version {cite('robin')}.",
             "Einfacher Einstieg: eingecheckt und auf jedem Agenten ohne CODESYS lesbar. Zeigt die "
             "Standardauflösung, nicht die effektive."],
            ["Scripting", f"CODESYS ohne Oberfläche ({C('--noUI --runscript')}) liest Gerätekennung und "
             f"Bibliotheksverweise einschließlich der effektiven Auflösung {cite('script')}.",
             "Genauester Weg; braucht Windows, eine CODESYS-Installation und die passenden Repositories "
             "auf dem Build-Agenten."],
        ],
        [0.22, 0.45, 0.33],
    )
    s.append(P(
        "In der Praxis empfehlen wir einen Zwischenschritt: Ersetzen Sie Verweise auf die neueste Version "
        "durch feste Versionen und halten Sie die Bibliotheks-Repositories der Build-Agenten im selben "
        "Stand wie die Arbeitsplätze. Dann stimmen Standard- und effektive Auflösung überein, und der "
        "PLCopen-Export reicht für eine belastbare SBOM."))

    # 6 CODESYS 4 --------------------------------------------------------
    s += H1("CODESYS 4: Textdateien und Lock-Datei")
    s.append(P(
        "CODESYS 4 bringt die für eine SBOM günstigste Projektablage mit: lesbare Textdateien und eine "
        "Lock-Datei, die jede aufgelöste Bibliothek mit exakter Version und Prüfsumme festhält. Version "
        f"1.0.0.0 ist seit dem 24. September 2026 verfügbar {cite('c4news')}. CODESYS stellt sie als zweites "
        "Werkzeug neben V3.5, nicht als Ersatz."))
    s += H2("6.1 Projektablage")
    s.append(P(
        "Ein Projekt ist ein Verzeichnis mit UTF-8-Textdateien, eine Datei je Objekt. IEC-Quelltext liegt "
        f"als Text vor, strukturierte Daten als JSON, Gerätebeschreibungen als XML {cite('c4help')}. Die Geräte "
        f"stehen in {C('DeviceTree.json')}. Ein zentrales Geräte-Repository gibt es nicht; die Beschreibungen "
        f"kommen aus Erweiterungen und einem Cache im Projekt {cite('c4switch')}. Das Verzeichnis lässt sich "
        "ohne Umweg mit Git versionieren."))
    s += H2("6.2 Bibliotheken und Lock-Datei")
    s.append(P(
        f"{C('Libraries.json')} beschreibt die gewünschten Bibliotheksverweise. {C('Libraries.lock.json')} "
        "„records the specific resolution of all direct and indirect dependencies“, mit exakter Version und "
        f"SHA384-Prüfsumme {cite('c4help')}. Aufgelöst wird nur, wenn der Anwender es auslöst; die "
        f"Repositories stehen in {C('LibraryRepositories.prefs.json')} {cite('c4switch')}."))
    s.append(P(
        "Für die SBOM ist die Lock-Datei die beste Quelle aller drei Generationen. Sie nennt auch indirekte "
        "Abhängigkeiten, die in V3.5 nur über Scripting sichtbar werden, und der Build-Agent löst dieselben "
        "Versionen auf wie der Arbeitsplatz. Das Problem aus Abschnitt 5.2 entfällt."))
    s += H2("6.3 Verhältnis zu V3.5")
    s.append(P(
        "CODESYS 4 nutzt dieselbe Laufzeit wie CODESYS 3; übersetzte Bibliotheken sind in beide Richtungen "
        "austauschbar. Auf der Steuerung ist mindestens CODESYS V3.5 SP16 nötig "
        f"{cite('c4help')}. V3.5-Projekte lassen sich nicht direkt öffnen. Die erste Version zielt vor allem "
        "auf Bibliotheksentwicklung; Visualisierung, Motion und Safety bleiben vorerst in V3.5 "
        f"{cite('c4news')}. Eine Scripting-Schnittstelle wie in V3.5 gibt es nicht. An ihre Stelle treten der "
        f"direkte Dateizugriff und die Kommandozeile {C('c4-cli')} {cite('c4switch')}."))
    s += H2("6.4 Zwei Einschränkungen")
    s += bullets([
        "Das Dateiformat ist nicht als Schnittstelle spezifiziert, und CODESYS plant etwa alle zwei Monate "
        f"eine neue Version {cite('c4help')}. Ein Leser muss deshalb die CODESYS-4-Version jeder Quelle prüfen "
        "und unbekannte Versionen melden, statt still falsch zu lesen.",
        "SHA384 ist nicht der Hash, den die TR-03183-2 verlangt. Er belegt die Identität der Bibliothek und "
        "kann zusätzlich in die SBOM; den SHA-512 der ausgelieferten Form ersetzt er nicht.",
    ])

    # 7 Vergleich --------------------------------------------------------
    s += H1("Die Unterschiede im Überblick")
    s.append(P(
        "Von V2.3 über V3.5 zu CODESYS 4 wird die Bibliotheksangabe genauer und die Projektablage offener. "
        "Die Firmware der Geräte steht in keiner Generation im Projekt."))
    s += table(
        ["Merkmal", "CODESYS V2.3", "CODESYS V3.5", "CODESYS 4"],
        [
            ["Status (Oktober 2026)", "Servicephase seit 01.07.2023", "aktuelle Hauptlinie",
             "Version 1.0.0.0 seit 24.09.2026"],
            ["Projektablage", f"eine Datei {C('*.pro')}, Format nicht offen", f"eine Binärdatei {C('*.project')}",
             "Verzeichnis mit Text- und JSON-Dateien"],
            ["Lesbarer Export", f"ASCII-Export {C('*.exp')}", f"CODESYS-XML {C('*.export')}, PLCopen XML",
             "nicht nötig, Ablage selbst ist lesbar"],
            ["Steuerung", f"Zielsystem aus TSP ({C('*.trg')}, {C('*.tnf')})",
             f"Gerätebeschreibung {C('*.devdesc.xml')} mit Typ, Id, Version",
             f"{C('DeviceTree.json')}, Beschreibung im Projekt-Cache"],
            ["Bibliotheksversion", f"Zeitstempel der {C('*.lib')}", "Version und Hersteller, Platzhalter, "
             "effektive Version", f"exakte Version und SHA384 in {C('Libraries.lock.json')}"],
            ["Indirekte Bibliotheken", "nicht ausgewiesen", "nur über Scripting oder Bibliotheksverwalter",
             "in der Lock-Datei"],
            ["Feldbusgeräte", f"{C('*.cfg')}, GSD, EDS", "Gerätebeschreibungen im Gerätebaum",
             "Gerätebeschreibungen im Projekt"],
            ["Firmware im Projekt", "nein", "nein", "nein"],
            ["Automatischer Export", f"{C('/batch /cmd')} (Windows)", f"Scripting {C('--noUI')} (Windows)",
             f"{C('c4-cli')}, direkter Dateizugriff"],
            ["Lesen ohne CODESYS auf dem Agenten", f"ja, aus eingechecktem {C('.exp')}",
             "ja, aus eingechecktem PLCopen XML", "ja, direkt aus dem Repository"],
            ["Größte Lücke für die SBOM", "keine Versionsnummern", "Auflösung hängt vom Repository des "
             "Rechners ab", "Format kann sich mit jeder Version ändern"],
        ],
        [0.22, 0.25, 0.27, 0.26],
    )

    # 8 Lücken -----------------------------------------------------------
    s += H1("Lücken vermerken, nicht auffüllen")
    s.append(P(
        "Eine SBOM aus CODESYS-Projekten hat Lücken. Das ist kein Fehler, solange sie benannt sind. Ein "
        "Fehler wäre, sie mit plausiblen Werten zu füllen: eine Firmware aus dem Typnamen geraten, die "
        "Version der Gerätebeschreibung als Firmware ausgegeben, ein Platzhalter-Hash, damit der Validator "
        "Ruhe gibt. Solche Werte bestehen jede Formatprüfung. Beim Abgleich mit Sicherheitshinweisen "
        "erzeugen sie falsche Treffer oder verdecken echte."))
    s.append(P(
        "CycloneDX bietet für ehrliche Lücken zwei Mittel: Properties an der betroffenen Komponente und den "
        f"Abschnitt {C('compositions')}, der die Vollständigkeit als {C('incomplete')} oder {C('unknown')} "
        f"kennzeichnet {cite('cdx')}. Die typischen Lücken bei CODESYS-Projekten:"))
    s += table(
        ["Lücke", "Betrifft", "Was sie schließt"],
        [
            ["Firmware nicht im Projekt", "alle Generationen, alle Geräte",
             "Ist-Stand aus Inbetriebnahme oder Service, als Datei eingelesen"],
            ["Bibliotheksversion nur als Zeitstempel", "V2.3",
             f"{C('.lib')}-Datei im Repository ablegen und hashen"],
            ["Bibliotheksversion nicht aufgelöst (neueste Version, Platzhalter)", "V3.5",
             "feste Versionen im Projekt oder effektive Auflösung per Scripting"],
            ["Kein SHA-512 der ausgelieferten Form", "Hersteller-Firmware, Laufzeit",
             "nach TR-03183-2 zulässig, wenn die Art der Zusammensetzung keinen Hash erlaubt; als Hinweis "
             "vermerken"],
            ["Keine Hersteller-SBOM", "Laufzeit und Firmware des Steuerungsherstellers",
             "Hersteller-SBOM anfordern und einbinden, sobald verfügbar"],
            ["Distributionslizenz unbekannt", "Bibliotheken Dritter",
             "Lizenz beim Lieferanten klären und in der Konfiguration erklären"],
        ],
        [0.33, 0.27, 0.40],
    )
    s.append(P(
        "Was Sie selbst ergänzen, etwa eine Lizenz oder einen Hersteller, ist eine eigene Quelle und wird in "
        "der SBOM als „deklariert“ gekennzeichnet. So bleibt erkennbar, welcher Wert aus dem Projekt stammt "
        "und welcher aus einer Erklärung. Prüfer und Kunden fragen genau danach."))

    # 9 Pipeline ---------------------------------------------------------
    s += H1("SBOM in der CI-Pipeline")
    s.append(P(
        "Die SBOM entsteht am besten in jedem Build, aus denselben Quellen wie die Software und gebunden an "
        "den Git-Commit. Dann gehört zu jedem Release genau eine SBOM, und sie lässt sich nach Jahren noch "
        "einem Stand zuordnen. Eine SBOM, die einmal im Jahr von Hand entsteht, ist am Tag nach der "
        "nächsten Änderung veraltet."))
    s += figure(diagram_pipeline(),
                "Ablauf in der Pipeline für Maschinen mit mehreren CODESYS-Generationen. Das Prüf-Gate "
                "unterscheidet Entwicklungs- und Release-Build.")
    s += bullets([
        f"<b>Quelle bereitstellen.</b> V2.3: {C('.exp')}-Export einchecken oder im Batch-Modus auf einem "
        "Windows-Agenten erzeugen. V3.5: PLCopen-XML-Export einchecken oder per Scripting erzeugen. "
        f"CODESYS 4: Projektverzeichnis und {C('Libraries.lock.json')} liegen bereits im Repository.",
        "<b>Je Station auslesen.</b> Steuerung, Feldbusgeräte, Bibliotheken und Werkzeugversion in ein "
        "gemeinsames Modell übernehmen. Die Version der Gerätebeschreibung bleibt als Eigenschaft erhalten, "
        "nicht als Firmware.",
        "<b>Zusammenführen.</b> Stationen unter der Maschine einordnen, Hersteller-SBOMs und Teil-SBOMs "
        "zuordnen, das Anwenderprogramm mit Release, Commit und SHA-512 über die Quellen ergänzen.",
        "<b>Prüfen.</b> Schema CycloneDX 1.6 und Pflichtfelder der TR-03183-2. Im Entwicklungs-Build sind "
        "vermerkte Lücken Warnungen, im Release-Build brechen sie den Build ab.",
        "<b>Ablegen.</b> SBOM, Prüfbericht und Ergebnis archivieren und optional an Dependency-Track "
        "übergeben, das die SBOM laufend gegen neue Schwachstellendaten prüft.",
    ])
    s.append(Spacer(1, 4))
    s.append(P(
        "Die Ausgabe muss reproduzierbar sein: gleiche Eingabe, gleiche Datei, Byte für Byte. Dazu gehören "
        "eine feste Sortierung, stabile Kennungen je Komponente und ein Zeitstempel aus dem Commit statt aus "
        "der Uhr des Agenten. Wie eine solche Pipeline mit Jenkins im Detail aussieht, beschreibt unser "
        f"Whitepaper zu TIA Portal V21 {cite('tiawp')}; der Ablauf lässt sich auf CODESYS übertragen."))

    # 10 Fazit -----------------------------------------------------------
    s += H1("Fazit")
    s.append(P(
        "Für alle drei CODESYS-Generationen lässt sich eine SBOM automatisch und ohne erfundene Werte "
        "erzeugen. Der Aufwand fällt nur an verschiedenen Stellen an. V2.3 braucht einen Textexport und die "
        f"{C('.lib')}-Dateien im Repository, sonst bleibt von jeder Bibliothek nur ein Zeitstempel. V3.5 "
        "liefert Versionen, aber erst feste Verweise oder die effektive Auflösung machen die SBOM unabhängig "
        "vom Build-Rechner. CODESYS 4 hat mit der Lock-Datei die beste Grundlage und verlangt dafür einen "
        "Leser, der mit jeder neuen Version Schritt hält."))
    s.append(P(
        "In keiner Generation steht die Firmware der Geräte im Projekt. Der projektierte Stand ist die halbe "
        "Antwort, den Ist-Stand liefert die Inbetriebnahme. Wer beides abgleicht, findet Abweichungen, bevor "
        "ein Sicherheitshinweis sie aufdeckt. Bis zum 11. Dezember 2027 bleibt gut ein Jahr. Fangen Sie bei "
        "den V2.3-Maschinen an, die weiter ausgeliefert werden: Dort müssen Exporte und Bibliotheksdateien "
        "erst zusammengetragen werden."))

    # 11 CTA -------------------------------------------------------------
    s += H1("Ihr nächster Schritt mit Comquent")
    s.append(P(
        "Comquent begleitet seit 2006 Unternehmen aus Fertigung, Automotive und Automatisierungstechnik bei "
        "CI/CD, DevSecOps und Industrial DevOps. Bei der SBOM für Ihre CODESYS-Maschinen unterstützen wir "
        "Sie in jeder Phase:"))
    s += table(
        ["Leistung", "Was Sie erhalten"],
        [
            ["SBOM-Readiness-Check", "Bestandsaufnahme Ihrer Maschinentypen, CODESYS-Generationen, Exporte und "
             "Lieferanten; Ergebnis ist ein SBOM-Konzept mit Fahrplan bis Dezember 2027."],
            ["Pilot-SBOM", "Eine vollständige CycloneDX-SBOM für eine Ihrer Maschinen, mit Lückenliste und dem "
             "Weg, jede Lücke zu schließen."],
            ["PLC SBOM Builder in Ihrer Pipeline", "Unser Kommandozeilenwerkzeug erzeugt und prüft die SBOM in "
             "Jenkins, GitLab CI oder Azure DevOps, auch für Maschinen mit Steuerungen mehrerer Hersteller."],
            ["Schwachstellenüberwachung", "Aufbau von Dependency-Track und CSAF-Import für Ihre Maschinen im Feld."],
            ["Schulung über die Comquent Academy", "SBOM und CRA für Automatisierungsteams, praxisnah in einer "
             "eigenen Cloud-Schulungsumgebung."],
        ],
        [0.3, 0.7],
    )
    s.append(P(
        f"Der <b>PLC SBOM Builder</b> {cite('product')} liest CODESYS-Projekte neben TIA Portal, Studio 5000 "
        "und TwinCAT, führt alles zu einer SBOM je Maschine zusammen, prüft die Pflichtfelder der "
        "TR-03183-2 als Build-Gate und schreibt einen lesbaren Prüfbericht mit allen Lücken. Der Adapter für "
        "CODESYS V3.5 entsteht zuerst; V2.3 und CODESYS 4 folgen nach einer Prüfung an echten Projekten. "
        "Dafür suchen wir Pilotkunden, besonders mit Bestandsprojekten auf V2.3 und ersten Projekten auf "
        "CODESYS 4."))
    s += cta_box("Sprechen Sie mit uns über Ihre SBOM", [
        "In einem Erstgespräch klären wir, welche CODESYS-Generationen in Ihren Maschinen laufen, welche "
        "Exporte es schon gibt und welcher Pilotumfang sinnvoll ist.",
        "<font color='#f5a623'><b>Comquent GmbH</b></font> · Lindberghstr. 7 · 82178 Puchheim",
        "Telefon +49 89 9393 3840 · info@comquent.de",
        "<link href='https://comquent.de/produkte/plc-sbom-builder' color='#ffffff'><u>comquent.de/produkte/"
        "plc-sbom-builder</u></link> · <link href='https://comquent.academy' color='#ffffff'><u>comquent.academy"
        "</u></link>",
    ])

    # 12 Glossar ---------------------------------------------------------
    s += H1("Glossar")
    s += table(
        ["Begriff", "Erklärung"],
        [
            [C("c4-cli"), "Kommandozeile von CODESYS 4; übersetzt Projekte ohne Oberfläche."],
            ["CODESYS Control", "Laufzeitsystem von CODESYS auf der Steuerung."],
            ["CPE", "Common Platform Enumeration, Kennung für Produkte in Schwachstellendatenbanken."],
            ["CRA", "Cyber Resilience Act, Verordnung (EU) 2024/2847 über Cybersicherheitsanforderungen an "
             "Produkte mit digitalen Elementen."],
            ["CSAF", "Common Security Advisory Framework, maschinenlesbares Format für Sicherheitshinweise von "
             "Herstellern."],
            ["CycloneDX", "SBOM-Format der OWASP Foundation, hier in Version 1.6 als JSON."],
            ["Effektive Version", "In V3.5 die Bibliotheksversion, die nach Auflösung aller Platzhalter "
             "tatsächlich in die Applikation geladen wird."],
            ["EDS, GSD, GSDML", "Gerätebeschreibungsdateien für CANopen, PROFIBUS und PROFINET."],
            ["Gerätebeschreibung", f"Datei, mit der die Engineering-Umgebung ein Gerät kennt ({C('*.devdesc.xml')} "
             "in V3.5); ihre Version ist nicht die Firmware."],
            ["Ist-Stand", "Im Feld ausgelesener Stand, etwa bei Inbetriebnahme oder Service."],
            ["Lock-Datei", f"In CODESYS 4 {C('Libraries.lock.json')} mit allen aufgelösten Bibliotheken, "
             "Versionen und Prüfsummen."],
            ["Lücke", "Pflichtangabe, die keine Quelle liefert; wird in der SBOM vermerkt, nicht aufgefüllt."],
            ["Platzhalter", "In V3.5 ein Bibliotheksverweis, dessen Ziel erst die Gerätebeschreibung oder der "
             "Anwender festlegt."],
            ["PLCopen XML", "Herstellerneutrales XML-Austauschformat für IEC-61131-3-Projekte."],
            ["SBOM", "Software Bill of Materials, Stückliste der Softwarebestandteile eines Produkts."],
            ["Soll-Stand", "Im Engineering-Projekt projektierter Stand."],
            ["Station", "Eine Steuerung mit allem, was an ihr hängt."],
            ["TR-03183-2", "Technische Richtlinie des BSI zu SBOM-Format und Pflichtfeldern, aktuell Version 2.1.0."],
            ["TSP", "Target Support Package; beschreibt in V2.3 das Zielsystem."],
        ],
        [0.24, 0.76],
    )

    # 13 Referenzen ------------------------------------------------------
    s += H1("Referenzen")
    s.append(P(f"Abrufdatum aller Online-Quellen: {ABRUF}, soweit nicht anders angegeben.", S_SMALL))
    s.append(Spacer(1, 6))
    for i, k in enumerate(_ref_order, start=1):
        text, url = REFS[k]
        s.append(P(f"[{i}]&nbsp;&nbsp;{text} <link href='{url}' color='#0e2a4a'>{url}</link>", S_REF))
    unused = [k for k in REFS if k not in _ref_order]
    if unused:
        raise SystemExit(f"Nicht zitierte Quellen: {unused}")
    s.append(Spacer(1, 14))
    s.append(P(
        "CODESYS ist eine Marke der CODESYS GmbH. WAGO-I/O-PRO ist eine Marke der WAGO GmbH &amp; Co. KG. "
        "CycloneDX ist ein Projekt der OWASP Foundation, SPDX ein Projekt der Linux Foundation. Alle weiteren "
        f"genannten Marken sind Eigentum der jeweiligen Inhaber. Stand der Angaben: {STAND}. Die Angaben "
        "entsprechen dem Stand der genannten Quellen und stellen keine Rechtsberatung dar; Produktfunktionen "
        "und regulatorische Vorgaben können sich ändern.", S_SMALL))
    return s


def build(target):
    global _ref_order
    _ref_order = []
    _fig[0] = 0
    CHAPTERS.clear()
    doc = BaseDocTemplate(
        target, pagesize=A4,
        leftMargin=MARGIN_X, rightMargin=MARGIN_X, topMargin=30 * mm, bottomMargin=22 * mm,
        title=f"{TITLE}: V2.3, V3.5 und CODESYS 4",
        author="Comquent GmbH", creator="Comquent GmbH",
        subject="Whitepaper: SBOM nach Cyber Resilience Act und BSI TR-03183-2 für CODESYS V2.3, V3.5 und "
                "CODESYS 4 (Stand Oktober 2026)",
        keywords="SBOM, CODESYS, CODESYS 4, CODESYS V3.5, CODESYS V2.3, CycloneDX, Cyber Resilience Act, "
                 "TR-03183-2, PLCopen XML, Industrial DevOps",
    )
    cover_frame = Frame(MARGIN_X, 18 * mm, CONTENT_W, PAGE_H - 36 * mm, id="cover",
                        leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    body = Frame(MARGIN_X, 22 * mm, CONTENT_W, PAGE_H - 52 * mm, id="body",
                 leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    doc.addPageTemplates([
        PageTemplate(id="cover", frames=[cover_frame], onPage=draw_cover),
        PageTemplate(id="body", frames=[body], onPage=draw_body),
    ])
    cover_story = cover()
    body_story = build_body()
    story = cover_story + [NextPageTemplate("body"), PageBreak()] + toc(CHAPTERS) + [PageBreak()] + body_story
    doc.build(story)
    return doc.page


def main():
    TOTAL_PAGES[0] = build(io.BytesIO())
    pages = build(str(OUTPUT))
    print(f"geschrieben: {OUTPUT} ({pages} Seiten)")


if __name__ == "__main__":
    main()
