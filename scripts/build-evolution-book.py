#!/usr/bin/env python3
"""Render the editable SanchesTV evolution book using Python and ReportLab.

Run from any directory: python scripts/build-evolution-book.py --strict-assets
Content and final integration facts live in docs/book. The renderer has no network
dependency and never modifies the application or any existing documentation.
"""

from __future__ import annotations

import argparse
import html
import json
import re
from pathlib import Path

from PIL import Image as PillowImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    Image,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
BOOK_DIR = ROOT / "docs" / "book"
PAGE_W, PAGE_H = A4
MARGIN = 52
CONTENT_W = PAGE_W - 2 * MARGIN
INK = colors.HexColor("#152430")
MUTED = colors.HexColor("#566675")
PAPER = colors.HexColor("#F5F2EB")
TEAL = colors.HexColor("#007D87")
CYAN = colors.HexColor("#60D5DB")
GOLD = colors.HexColor("#DAA25C")
PALE = colors.HexColor("#E6EBE8")


def register_fonts(font_dir: Path | None) -> None:
    candidates = [font_dir] if font_dir else []
    candidates += [
        Path("/usr/share/fonts/truetype/dejavu"),
        Path("/usr/local/share/fonts/dejavu"),
        Path("C:/Windows/Fonts"),
    ]
    files = {
        "Book": "DejaVuSans.ttf",
        "BookBold": "DejaVuSans-Bold.ttf",
        "BookItalic": "DejaVuSans-Oblique.ttf",
        "BookBoldItalic": "DejaVuSans-BoldOblique.ttf",
        "BookMono": "DejaVuSansMono.ttf",
    }
    for folder in candidates:
        if folder and all((folder / filename).is_file() for filename in files.values()):
            for name, filename in files.items():
                pdfmetrics.registerFont(TTFont(name, str(folder / filename)))
            pdfmetrics.registerFontFamily(
                "Book", normal="Book", bold="BookBold", italic="BookItalic",
                boldItalic="BookBoldItalic",
            )
            return
    raise RuntimeError(
        "Unicode fonts missing. Install fonts-dejavu-core or pass --font-dir "
        "with DejaVuSans*.ttf and DejaVuSansMono.ttf."
    )


def make_styles() -> dict[str, ParagraphStyle]:
    base = dict(fontName="Book", textColor=INK, alignment=TA_LEFT)
    return {
        "body": ParagraphStyle("body", fontSize=10.25, leading=15.0, spaceAfter=10.3, **base),
        "title": ParagraphStyle(
            "title", fontSize=25, leading=30, spaceAfter=19,
            fontName="BookBold", textColor=INK,
        ),
        "eyebrow": ParagraphStyle(
            "eyebrow", fontSize=8, leading=12, spaceAfter=10,
            fontName="BookBold", textColor=TEAL,
        ),
        "quote": ParagraphStyle(
            "quote", fontSize=10.4, leading=15.4, spaceAfter=1, **base,
        ),
        "table": ParagraphStyle("table", fontSize=8.8, leading=12.6, **base),
        "table_head": ParagraphStyle(
            "table_head", fontSize=8.8, leading=12.6, fontName="BookBold", textColor=colors.white,
        ),
        "caption": ParagraphStyle(
            "caption", fontSize=8.3, leading=12.6, spaceAfter=10.5,
            fontName="Book", textColor=MUTED,
        ),
        "cover_title": ParagraphStyle(
            "cover_title", fontName="BookBold", fontSize=35, leading=42, textColor=colors.white,
        ),
        "cover_body": ParagraphStyle(
            "cover_body", fontName="Book", fontSize=11.2, leading=17.3, textColor=colors.HexColor("#D5DEE3"),
        ),
    }


def inline(text: str) -> str:
    escaped = html.escape(text, quote=False)
    escaped = re.sub(
        r"\[([^\]]+)\]\((https://[^)]+)\)",
        lambda match: '<link href="' + html.escape(html.unescape(match[2]), quote=True)
        + '" color="#007D87">' + match[1] + '</link>',
        escaped,
    )
    escaped = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", escaped)
    escaped = re.sub(r"`(.+?)`", r'<font name="BookMono">\1</font>', escaped)
    return escaped


def resolve_asset(facts: dict, key: str) -> Path:
    raw = facts.get(key, "")
    return ROOT / raw if raw else ROOT / "__missing_asset__"


def draw_wrapped(canvas, text, x, y, width, style) -> float:
    paragraph = Paragraph(inline(text), style)
    _, height = paragraph.wrap(width, 1000)
    paragraph.drawOn(canvas, x, y - height)
    return height


class Diagram(Flowable):
    """Small vector diagrams, kept selectable and sharp in the PDF."""

    def __init__(self, name: str):
        super().__init__()
        self.name = name
        self.width = CONTENT_W
        self.height = 135 if name == "architecture" else 105

    def wrap(self, avail_width, avail_height):
        self.width = min(CONTENT_W, avail_width)
        return self.width, self.height

    def arrow(self, x1, y1, x2, y2):
        c = self.canv
        c.setStrokeColor(TEAL)
        c.setLineWidth(1.2)
        c.line(x1, y1, x2, y2)
        p = c.beginPath()
        p.moveTo(x2, y2)
        p.lineTo(x2 - 4, y2 + 3)
        p.lineTo(x2 - 4, y2 - 3)
        p.close()
        c.setFillColor(TEAL)
        c.drawPath(p, fill=1, stroke=0)

    def box(self, x, y, w, h, title, detail):
        c = self.canv
        c.setFillColor(INK)
        c.roundRect(x, y, w, h, 7, stroke=0, fill=1)
        c.setFillColor(CYAN)
        c.setFont("BookBold", 9)
        c.drawString(x + 11, y + h - 20, title)
        c.setFillColor(colors.HexColor("#DAE2E6"))
        c.setFont("Book", 7.7)
        for i, line in enumerate(detail.split("\n")):
            c.drawString(x + 11, y + h - 36 - 11 * i, line)

    def draw(self):
        c, w = self.canv, self.width
        if self.name == "architecture":
            rows = [
                ("DESKTOP · WPF / .NET 10", "Janelas, player nativo, áudio Windows e processos externos"),
                ("CORE · .NET 8", "Catálogo, descoberta, políticas, SQLite e workflows"),
                ("TESTS · xUNIT", "Regras e regressões automatizadas do núcleo"),
            ]
            for i, (title, detail) in enumerate(rows):
                self.box(0, 91 - i * 42, w, 37, title, "")
                c.setFont("Book", 7.7)
                c.setFillColor(colors.HexColor("#DAE2E6"))
                c.drawString(190, 103 - i * 42, detail)
            return
        configs = {
            "principles": [
                ("CONTINUIDADE", "Funções preservadas\nRotas existentes"),
                ("DESCOBERTA", "Catálogo local\nInterface integrada"),
                ("EVIDÊNCIA", "Testes e builds\nLimites declarados"),
            ],
            "playback": [
                ("FONTE", "Canal escolhido\nURLs conhecidas"),
                ("SAÚDE", "Histórico local\nOrdem de tentativa"),
                ("LIBMPV", "Motor preferido\nVídeo nativo"),
                ("LIBVLC", "Alternativa\nSe necessário"),
            ],
            "live": [
                ("XMLTV", "Título e horários\nGuia do canal"),
                ("AO VIVO", "Player + timeshift\nJanela local"),
                ("GRAVAÇÃO", "FFmpeg + SQLite\nManual / agendada"),
            ],
            "workflow": [
                ("DATABASE", "Abrir banco\nEstado local"),
                ("SEED", "Preparar base\nRetry / timeout"),
                ("CHANNELS", "Carregar catálogo\nSnapshot do fluxo"),
                ("HOME", "Abrir experiência\nHistórico de eventos"),
            ],
            "discovery": [
                ("ENTRADAS", "Canais, EPG\nFavoritos / recentes"),
                ("REGRAS LOCAIS", "Afinidade e diversidade\nSem API de IA"),
                ("CINEMA HUB", "Cards e programas\nPlayer existente"),
            ],
            "distribution": [
                ("CÓDIGO", "Build + testes\nPublish win-x64"),
                ("RUNTIMES", "Proveniência\nSHA-256"),
                ("PACOTES", "Inno / Velopack\nInstalação limpa"),
                ("PUBLICAÇÃO", "Self-test + hashes\nGitHub Release"),
            ],
        }
        nodes = configs.get(self.name)
        if not nodes:
            raise ValueError(f"Unknown diagram: {self.name}")
        gap, count = 14, len(nodes)
        bw = (w - gap * (count - 1)) / count
        for i, (title, detail) in enumerate(nodes):
            self.box(i * (bw + gap), 22, bw, 76, title, detail)
            if i < count - 1:
                self.arrow(i * (bw + gap) + bw + 2, 59, (i + 1) * (bw + gap) - 2, 59)


class Book(BaseDocTemplate):
    def __init__(self, path, facts, cover_content, styles):
        super().__init__(
            str(path), pagesize=A4, leftMargin=MARGIN, rightMargin=MARGIN,
            topMargin=62, bottomMargin=58, allowSplitting=0,
            title="SanchesTV — A evolução de uma experiência de cinema",
            author="Projeto SanchesTV", subject="História, arquitetura, evidências e direção visual",
            creator="Python + ReportLab · scripts/build-evolution-book.py", invariant=1,
        )
        self.facts = facts
        self.cover_content = cover_content
        self.styles = styles
        self.rendered_pages = []
        self.title_positions = []
        frame = Frame(MARGIN, 58, CONTENT_W, PAGE_H - 120, leftPadding=0, rightPadding=0,
                      topPadding=0, bottomPadding=0)
        self.addPageTemplates(PageTemplate(id="book", frames=[frame], onPage=self.page_background))

    def afterFlowable(self, flowable):
        if isinstance(flowable, Paragraph) and flowable.style.name == "title":
            self.title_positions.append((self.page, flowable.getPlainText()))

    def page_background(self, c, doc):
        self.rendered_pages.append(doc.page)
        c.saveState()
        c.setFillColor(PAPER)
        c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
        if doc.page == 1:
            self.cover(c)
            c.restoreState()
            return
        c.setFillColor(INK)
        c.rect(0, PAGE_H - 10, PAGE_W, 10, fill=1, stroke=0)
        c.setFillColor(TEAL)
        c.rect(MARGIN, PAGE_H - 10, 72, 10, fill=1, stroke=0)
        c.setFont("BookBold", 7.3)
        c.setFillColor(MUTED)
        c.drawString(MARGIN, PAGE_H - 34, "SANCHESTV  /  EVOLUÇÃO")
        c.setFont("Book", 7.3)
        c.drawRightString(PAGE_W - MARGIN, PAGE_H - 34, "HISTÓRIA · ENGENHARIA · DESIGN")
        c.setStrokeColor(colors.HexColor("#D1D7D4"))
        c.setLineWidth(.6)
        c.line(MARGIN, 42, PAGE_W - MARGIN, 42)
        c.setFillColor(MUTED)
        c.setFont("Book", 7.1)
        c.drawString(MARGIN, 28, "Edição " + str(self.facts["edition_date"]))
        c.setFont("BookBold", 8)
        c.drawRightString(PAGE_W - MARGIN, 28, f"{doc.page:02d}")
        c.restoreState()

    def cover(self, c):
        c.setFillColor(INK)
        c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
        art = resolve_asset(self.facts, "cover_art")
        if art.is_file():
            with PillowImage.open(art) as im:
                iw, ih = im.size
            scale = max(PAGE_W / iw, PAGE_H / ih)
            width, height = iw * scale, ih * scale
            c.drawImage(ImageReader(str(art)), (PAGE_W - width) / 2,
                        (PAGE_H - height) / 2, width=width, height=height)
            c.saveState()
            c.setFillColor(colors.HexColor("#05121C"))
            c.setFillAlpha(.62)
            c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
            c.restoreState()
        else:
            c.setStrokeColor(colors.HexColor("#234355"))
            c.setLineWidth(1)
            for radius in range(80, 520, 38):
                c.circle(PAGE_W + 30, PAGE_H * .44, radius, fill=0, stroke=1)
        c.setFillColor(CYAN)
        c.rect(MARGIN, PAGE_H - 74, 63, 4, fill=1, stroke=0)
        c.setFont("BookBold", 8)
        c.drawString(MARGIN, PAGE_H - 100, "SANCHESTV · CADERNO DE ENGENHARIA E DESIGN")
        title = self.cover_content["title"]
        draw_wrapped(c, title, MARGIN, PAGE_H - 154, CONTENT_W - 15, self.styles["cover_title"])
        c.setFillColor(GOLD)
        c.setFont("BookBold", 20)
        c.drawString(MARGIN, 431, "SanchesTV")
        draw_wrapped(c, self.cover_content["paragraphs"][1], MARGIN, 390, CONTENT_W - 20,
                     self.styles["cover_body"])
        c.setStrokeColor(colors.HexColor("#64727C"))
        c.line(MARGIN, 221, PAGE_W - MARGIN, 221)
        draw_wrapped(c, self.cover_content["paragraphs"][2], MARGIN, 195, CONTENT_W,
                     self.styles["cover_body"])
        draw_wrapped(c, self.cover_content["paragraphs"][3], MARGIN, 130, CONTENT_W,
                     self.styles["cover_body"])
        c.setFillColor(CYAN)
        c.setFont("BookBold", 8)
        c.drawString(MARGIN, 45, f"UM REGISTRO DO PROJETO · {self.facts['page_count']} PÁGINAS")


def paragraph(text, styles, kind="body"):
    return Paragraph(inline(text), styles[kind])


def make_table(lines: list[str], styles):
    rows = [[cell.strip() for cell in line.strip().strip("|").split("|")] for line in lines]
    rows = [row for row in rows if not all(re.fullmatch(r":?-+:?", cell) for cell in row)]
    count = len(rows[0])
    widths = {
        2: [CONTENT_W * .20, CONTENT_W * .80],
        3: [CONTENT_W * .22, CONTENT_W * .24, CONTENT_W * .54],
    }.get(count, [CONTENT_W / count] * count)
    # The roadmap's evidence deserves more space than a short priority column.
    if rows[0][0] == "Prioridade":
        widths = [CONTENT_W * .12, CONTENT_W * .43, CONTENT_W * .45]
    if rows[0][0] == "Marco":
        widths = [CONTENT_W * .17, CONTENT_W * .15, CONTENT_W * .68]
    if rows[0][0] == "Página":
        widths = [CONTENT_W * .15, CONTENT_W * .85]
    if rows[0][0] == "Evidência":
        widths = [CONTENT_W * .28, CONTENT_W * .72]
    cells = [[paragraph(cell, styles, "table_head" if ri == 0 else "table")
              for cell in row] for ri, row in enumerate(rows)]
    table = Table(cells, colWidths=widths, hAlign="LEFT", repeatRows=1)
    padding = 4 if rows[0][0] == "Página" else 7
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), INK),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.HexColor("#EAEDE7"), PAPER]),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), padding),
        ("BOTTOMPADDING", (0, 0), (-1, -1), padding),
        ("LINEBELOW", (0, -1), (-1, -1), .6, colors.HexColor("#CFD8D3")),
    ]))
    return [table, Spacer(1, 14)]


def quote_box(text, styles):
    table = Table([[paragraph(text, styles, "quote")]], colWidths=[CONTENT_W])
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), PALE),
        ("LINEBEFORE", (0, 0), (0, 0), 3, TEAL),
        ("LEFTPADDING", (0, 0), (-1, -1), 14),
        ("RIGHTPADDING", (0, 0), (-1, -1), 14),
        ("TOPPADDING", (0, 0), (-1, -1), 11),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 11),
    ]))
    return [table, Spacer(1, 13)]


def screenshot_flowable(name, facts, styles, strict):
    asset = resolve_asset(facts, "screenshot_" + name)
    if not asset.is_file():
        if strict:
            raise FileNotFoundError(f"Missing screenshot: {asset}")
        return quote_box("Prévia visual reservada para a integração final: " + name, styles)
    with PillowImage.open(asset) as im:
        width, height = im.size
    max_height = 350 if name == "mobile" else 355
    max_width = CONTENT_W - 26 if name != "mobile" else CONTENT_W * .54
    scale = min(max_width / width, max_height / height)
    image = Image(str(asset), width=width * scale, height=height * scale)
    image.hAlign = "CENTER"
    border = Table([[image]], colWidths=[CONTENT_W])
    border.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), INK),
        ("ALIGN", (0, 0), (-1, -1), "CENTER"),
        ("BOX", (0, 0), (-1, -1), .7, colors.HexColor("#C2CDCB")),
        ("TOPPADDING", (0, 0), (-1, -1), 13),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 13),
        ("LEFTPADDING", (0, 0), (-1, -1), 13),
        ("RIGHTPADDING", (0, 0), (-1, -1), 13),
    ]))
    return [border, Spacer(1, 15)]


def substitute(source, facts):
    def replace(match):
        key = match.group(1)
        if key not in facts:
            raise KeyError(f"Unknown book fact: {key}")
        return str(facts[key])
    return re.sub(r"\{\{([a-z_]+)\}\}", replace, source)


def parse_page(source, facts, styles, strict):
    story, lines, i = [], source.splitlines(), 0
    while i < len(lines):
        line = lines[i].strip()
        if not line:
            i += 1
            continue
        eyebrow = re.fullmatch(r"<!-- eyebrow: (.+) -->", line)
        diagram = re.fullmatch(r"<!-- diagram:\s*(\w+) -->", line)
        image = re.fullmatch(r"<!-- image:\s*(\w+) -->", line)
        if eyebrow:
            story.append(paragraph(eyebrow[1], styles, "eyebrow"))
        elif line.startswith("# "):
            story.append(paragraph(line[2:], styles, "title"))
        elif diagram:
            story.extend([Diagram(diagram[1]), Spacer(1, 7)])
        elif image:
            story.extend(screenshot_flowable(image[1], facts, styles, strict))
        elif line.startswith("|"):
            table_lines = [line]
            while i + 1 < len(lines) and lines[i + 1].strip().startswith("|"):
                i += 1
                table_lines.append(lines[i].strip())
            story.extend(make_table(table_lines, styles))
        elif line.startswith("> "):
            story.extend(quote_box(line[2:], styles))
        elif line.startswith("<!--"):
            pass
        else:
            collected = [line]
            while i + 1 < len(lines) and lines[i + 1].strip():
                following = lines[i + 1].strip()
                if following.startswith(("# ", "|", "> ", "<!--")):
                    break
                i += 1
                collected.append(following)
            text = " ".join(collected)
            story.append(paragraph(text, styles, "caption" if text.startswith("**Legenda:") else "body"))
        i += 1
    return story


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=BOOK_DIR / "SanchesTV-Evolucao.md")
    parser.add_argument("--facts", type=Path, default=BOOK_DIR / "build-facts.json")
    parser.add_argument("--output", type=Path, default=ROOT / "docs" / "SanchesTV-Evolucao.pdf")
    parser.add_argument("--font-dir", type=Path)
    parser.add_argument("--strict-assets", action="store_true")
    args = parser.parse_args()
    register_fonts(args.font_dir)
    styles = make_styles()
    facts = json.loads(args.facts.read_text(encoding="utf-8"))
    facts["page_count"] = 24 if facts.get("include_native", False) else 23
    facts["native_index"] = (
        "| 24 | Captura nativa WPF no Windows |" if facts.get("include_native", False) else ""
    )
    if args.strict_assets:
        keys = ["cover_art", "screenshot_desktop", "screenshot_mobile", "screenshot_gallery"]
        if facts.get("include_native", False):
            keys.append("screenshot_native")
        for key in keys:
            asset = resolve_asset(facts, key)
            if not asset.is_file():
                raise FileNotFoundError(f"Required final asset missing: {asset}")
        for key in ["test_count", "test_result", "desktop_build_result", "core_addition", "native_addition"]:
            if any(marker in str(facts[key]) for marker in ("a confirmar", "será registrada", "após a integração")):
                raise ValueError(f"Integration fact still pending: {key}")
    source = substitute(args.source.read_text(encoding="utf-8"), facts)
    split = re.split(r"<!-- page:(\w+) -->", source)
    pages = [(split[i], split[i + 1]) for i in range(1, len(split), 2)]
    if not facts.get("include_native", False):
        pages = [page for page in pages if page[0] != "visual_native"]
    if len(pages) != facts["page_count"]:
        raise ValueError(f"Expected {facts['page_count']} editorial pages, found {len(pages)}")
    cover_source = pages[0][1]
    cover_title = re.search(r"^# (.+)$", cover_source, re.MULTILINE).group(1)
    cover_paragraphs = [p.strip() for p in re.split(r"\n\s*\n", cover_source)
                        if p.strip() and not p.lstrip().startswith(("<!--", "# "))]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    doc = Book(args.output, facts, {"title": cover_title, "paragraphs": cover_paragraphs}, styles)
    story = [Spacer(1, 1), PageBreak()]
    for index, (name, page) in enumerate(pages[1:], start=1):
        story.extend(parse_page(page, facts, styles, args.strict_assets))
        if index != len(pages) - 1:
            story.append(PageBreak())
    doc.build(story)
    if len(doc.rendered_pages) != len(pages):
        args.output.unlink(missing_ok=True)
        raise RuntimeError(
            f"Layout overflow: expected {len(pages)} pages, rendered {len(doc.rendered_pages)}. "
            f"Page starts: {doc.title_positions}. Reduce source text or adjust renderer before delivery."
        )
    display_path = args.output.relative_to(ROOT) if args.output.is_relative_to(ROOT) else args.output
    print(f"Generated {display_path} · {len(doc.rendered_pages)} pages · "
          f"{args.output.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
