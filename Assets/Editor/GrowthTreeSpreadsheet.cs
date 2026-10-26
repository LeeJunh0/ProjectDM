using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace ProjectDM.Editor
{
    internal static class GrowthTreeSpreadsheet
    {
        internal static readonly string[] Headers = { "treeId", "enabled", "id", "growthId", "name", "ranks", "parent", "parentRanks", "x", "y" };
        static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        internal static GrowthTreeSettings Import(string path, GrowthTreeSettings baseline = null)
        {
            List<List<string>> rows;
            if (Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)) rows = Csv(File.ReadAllText(path, Encoding.UTF8));
            else if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) rows = ReadXlsx(path);
            else throw new InvalidOperationException(".xlsx 또는 UTF-8 .csv 파일을 선택하세요. .xls는 Excel에서 .xlsx로 저장해야 합니다.");
            if (rows.Count == 0) throw new InvalidOperationException("Nodes 시트에 데이터가 없습니다.");
            var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows[0].Count; i++)
            {
                string header = rows[0][i].Trim().TrimStart('\ufeff');
                if (header.Length > 0 && !columns.TryAdd(header, i)) throw new InvalidOperationException("중복 열: " + header);
            }
            foreach (string header in Headers) if (!columns.ContainsKey(header)) throw new InvalidOperationException("Nodes 시트의 필수 열 누락: " + header);
            var result = ScriptableObject.CreateInstance<GrowthTreeSettings>();
            try
            {
                var groups = GrowthTreeSettings.TreeIds.ToDictionary(id => id, _ => new List<GrowthTreeNodeSettings>());
                for (int r = 1; r < rows.Count; r++)
                {
                    var row = rows[r]; if (row.All(string.IsNullOrWhiteSpace)) continue;
                    string Cell(string key) => columns.TryGetValue(key, out int column) && column < row.Count ? row[column].Trim() : "";
                    bool Bool(string key)
                    {
                        string value = Cell(key).ToLowerInvariant();
                        if (value == "" || value == "false" || value == "0") return false;
                        if (value == "true" || value == "1") return true;
                        throw new InvalidOperationException($"Nodes {r + 1}행 {key}: TRUE/FALSE 또는 1/0을 입력하세요.");
                    }
                    int Int(string key)
                    { if (int.TryParse(Cell(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n; throw new InvalidOperationException($"Nodes {r + 1}행 {key}: 정수가 필요합니다."); }
                    float Number(string key)
                    { if (float.TryParse(Cell(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && !float.IsNaN(n) && !float.IsInfinity(n)) return n; throw new InvalidOperationException($"Nodes {r + 1}행 {key}: 유한한 숫자가 필요합니다."); }
                    string tree = Cell("treeId");
                    if (!groups.ContainsKey(tree)) throw new InvalidOperationException($"Nodes {r + 1}행 treeId: common/projectile/flame/lightning/frost 중 하나여야 합니다.");
                    string enabled = Cell("enabled").ToLowerInvariant();
                    if (enabled != "true" && enabled != "false" && enabled != "1" && enabled != "0") throw new InvalidOperationException($"Nodes {r + 1}행 enabled: TRUE/FALSE 또는 1/0을 입력하세요.");
                    groups[tree].Add(new GrowthTreeNodeSettings { enabled = enabled == "true" || enabled == "1", id = Cell("id"),
                        growthId = Cell("growthId"), name = Cell("name"), ranks = Int("ranks"), parent = string.IsNullOrEmpty(Cell("parent")) ? "ROOT" : Cell("parent"),
                        parentRanks = Int("parentRanks"), x = Number("x"), y = Number("y"), displayParent = Cell("displayParent"), displayOrthogonal = Bool("displayOrthogonal"),
                        displayLinks = Cell("displayLinks").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).ToArray() });
                }
                result.trees = GrowthTreeSettings.TreeIds.Select((id, i) => new GrowthTreeDefinition { id = id, name = GrowthTreeSettings.TreeNames[i],
                    nodes = groups[id].Count > 0 || baseline == null ? groups[id].ToArray() :
                        JsonUtility.FromJson<GrowthTreeDefinition>(JsonUtility.ToJson(baseline.trees.First(tree => tree.id == id))).nodes }).ToArray();
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result); throw; }
        }

        static List<List<string>> ReadXlsx(string path)
        {
            using var stream = File.OpenRead(path); using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            if (zip.Entries.Sum(entry => entry.Length) > 32 * 1024 * 1024) throw new InvalidOperationException("성장트리 엑셀의 압축 해제 크기가 32MB를 초과합니다.");
            XDocument Xml(string entry)
            {
                var item = zip.GetEntry(entry) ?? throw new InvalidOperationException("엑셀 구성 파일 누락: " + entry);
                using var data = item.Open(); using var reader = XmlReader.Create(data, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                return XDocument.Load(reader);
            }
            var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? Array.Empty<string>() : Xml("xl/sharedStrings.xml").Descendants(S + "si")
                .Select(item => string.Concat(item.Descendants(S + "t").Select(text => text.Value))).ToArray();
            var sheet = Xml("xl/workbook.xml").Descendants(S + "sheet").FirstOrDefault(item => (string)item.Attribute("name") == "Nodes");
            if (sheet == null) throw new InvalidOperationException("Nodes 시트가 없습니다. 에디터의 엑셀 저장으로 양식을 먼저 만드세요.");
            string rel = (string)sheet.Attribute(R + "id");
            var relationship = Xml("xl/_rels/workbook.xml.rels").Root.Elements().FirstOrDefault(item => (string)item.Attribute("Id") == rel);
            if (relationship == null || (string)relationship.Attribute("TargetMode") == "External") throw new InvalidOperationException("Nodes 시트의 내부 연결이 올바르지 않습니다.");
            string target = (string)relationship.Attribute("Target");
            if (string.IsNullOrEmpty(target)) throw new InvalidOperationException("Nodes 시트 경로가 없습니다.");
            string entryPath = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            var rows = new List<List<string>>();
            foreach (var row in Xml(entryPath).Descendants(S + "sheetData").Elements(S + "row"))
            {
                int rowIndex = (int?)row.Attribute("r") ?? rows.Count + 1;
                if (rowIndex < 1 || rowIndex > 10000) throw new InvalidOperationException("Nodes 시트는 10,000행 이내여야 합니다.");
                while (rows.Count < rowIndex) rows.Add(new List<string>());
                var cells = rows[rowIndex - 1];
                foreach (var cell in row.Elements(S + "c"))
                {
                    string address = (string)cell.Attribute("r"); int column = 0;
                    foreach (char c in address ?? "") { if (c < 'A' || c > 'Z') break; column = column * 26 + c - 'A' + 1; }
                    if (column < 1 || column > 256) throw new InvalidOperationException("잘못된 셀 주소: " + address);
                    while (cells.Count < column) cells.Add("");
                    string type = (string)cell.Attribute("t"), value = (string)cell.Element(S + "v") ?? "";
                    if (type == "e") throw new InvalidOperationException("엑셀 수식 오류: " + address + " " + value);
                    if (cell.Element(S + "f") != null && cell.Element(S + "v") == null) throw new InvalidOperationException("수식의 계산값이 없습니다: " + address + ". Excel에서 다시 계산/저장하거나 값으로 붙여넣으세요.");
                    if (type == "s")
                    { if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Length) throw new InvalidOperationException("문자열 참조 오류: " + address); value = strings[index]; }
                    else if (type == "inlineStr") value = string.Concat(cell.Descendants(S + "t").Select(text => text.Value));
                    cells[column - 1] = value;
                }
            }
            return rows;
        }

        static List<List<string>> Csv(string text)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
                else if (!quoted && (c == ',' || c == '\r' || c == '\n'))
                {
                    row.Add(cell.ToString()); cell.Clear();
                    if (c != ',') { rows.Add(row); row = new List<string>(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
                }
                else cell.Append(c);
            }
            if (quoted) throw new InvalidOperationException("CSV의 따옴표가 닫히지 않았습니다.");
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        internal static void Export(string path, GrowthTreeSettings settings, Dictionary<string, GameTables> graphs)
        {
            var nodes = new List<object[]> { Headers.Concat(new[] { "displayParent", "displayOrthogonal", "displayLinks" }).Cast<object>().ToArray() };
            foreach (var tree in settings.trees) foreach (var n in tree.nodes)
                nodes.Add(new object[] { tree.id, n.enabled, n.id, n.growthId, n.name, n.ranks, n.parent, n.parentRanks, n.x, n.y, n.displayParent, n.displayOrthogonal, string.Join("|", n.displayLinks ?? Array.Empty<string>()) });
            var effects = new List<object[]> { new object[] { "treeId", "growthId", "name", "description", "maxRank", "baseCost", "costGrowth", "step", "requires" } };
            foreach (var tree in settings.trees) foreach (var row in graphs[tree.id].growthTable.rows)
                effects.Add(new object[] { tree.id, row.id, row.name, row.description, row.maxRank, row.baseCost, row.costGrowth, row.step, row.requires });
            var guide = new List<object[]> {
                new object[] { "성장트리 편집", "Nodes 시트만 가져옵니다. Effects는 구현된 성장 종류의 참고 목록입니다." },
                new object[] { "흐름", "엑셀 불러오기 → 그래프/연결 편집 → 게임에 저장. 불러오기만으로 게임 설정은 바뀌지 않습니다." },
                new object[] { "treeId", "common / projectile / flame / lightning / frost" },
                new object[] { "enabled", "TRUE인 노드만 생성합니다. 부모를 끄면 자식의 부모도 수정하세요." },
                new object[] { "id / growthId", "id는 전체 트리에서 고유한 노드 ID, growthId는 Effects의 효과 ID입니다." },
                new object[] { "ranks", "노드당 1~3회 구매. 같은 growthId는 Nodes 행 순서대로 누적하며 합계는 Effects.maxRank 이하여야 합니다." },
                new object[] { "parent / parentRanks", "해금 기준 부모와 필요 구매 단계입니다. parentRanks=0은 추가 해금 조건 없음, 1 이상은 실제 해금 조건입니다." },
                new object[] { "displayParent", "화면에 보이는 기본 연결. 격자 지도 정리는 격자 묶음을 가지와 분기로 이어 배치하며 해금 조건은 유지됩니다." },
                new object[] { "x / y", "게임 성장 지도 좌표입니다. 에디터에서 노드를 드래그해 바꿀 수 있습니다." },
                new object[] { "저장 호환", "기존 growthId별 구매 기록은 유지됩니다. 노드 재배치/분할 변경은 그 기록을 새 구간에 표시합니다." },
                new object[] { "기존 효과", "노드를 끄면 구매 경로만 숨깁니다. 이미 구매한 성장의 능력치와 저장 기록은 유지됩니다." },
                new object[] { "displayOrthogonal", "TRUE면 묶음 사이를 직각으로 꺾어 연결하고 FALSE/빈칸이면 직선입니다. 루트의 첫 연결은 대각선도 허용합니다." },
                new object[] { "displayLinks", "격자의 가로·세로 이웃을 잇는 추가 표시 연결입니다. 노드 ID를 |로 구분합니다. 고리 모양을 허용하며 구매·해금 조건에는 영향을 주지 않습니다." }
            };
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                void Write(string name, string value)
                { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(value); }
                string[] names = { "Nodes", "Effects", "Guide" }; var all = new[] { nodes, effects, guide };
                Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" + string.Concat(Enumerable.Range(1, 3).Select(i => $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) + "</Types>");
                Write("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                Write("xl/workbook.xml", new XDocument(new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R), new XElement(S + "sheets", names.Select((name, i) => new XElement(S + "sheet", new XAttribute("name", name), new XAttribute("sheetId", i + 1), new XAttribute(R + "id", "rId" + (i + 1))))))).ToString());
                Write("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + string.Concat(Enumerable.Range(1, 3).Select(i => $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>")) + "</Relationships>");
                for (int i = 0; i < all.Length; i++)
                {
                    var data = new XElement(S + "sheetData"); int number = 0;
                    foreach (object[] values in all[i])
                    {
                        var row = new XElement(S + "row", new XAttribute("r", ++number));
                        for (int col = 0; col < values.Length; col++)
                        {
                            object value = values[col] ?? ""; string address = ((char)('A' + col)).ToString() + number;
                            bool numeric = value is int || value is float;
                            row.Add(new XElement(S + "c", new XAttribute("r", address), new XAttribute("t", numeric ? "n" : value is bool ? "b" : "inlineStr"),
                                numeric || value is bool ? new XElement(S + "v", value is bool flag ? flag ? "1" : "0" : value is float numberValue ? numberValue.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture)) :
                                new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value.ToString()))));
                        }
                        data.Add(row);
                    }
                    var views = new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0),
                        new XElement(S + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen"))));
                    var columns = new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", 1), new XAttribute("max", 10), new XAttribute("width", 24), new XAttribute("customWidth", 1)));
                    Write($"xl/worksheets/sheet{i + 1}.xml", new XDocument(new XElement(S + "worksheet", views, columns, data)).ToString());
                }
            }
            File.WriteAllBytes(path, memory.ToArray());
        }
    }
}
