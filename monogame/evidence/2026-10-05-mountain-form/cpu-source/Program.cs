using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Yipe.Core;
using Yipe.Desktop;

var root = Directory.GetCurrentDirectory();
var directory = Path.Combine(root, "build/mountain-form-headless");
using var maps = File.OpenRead(Path.Combine(root, "assets/maps/original-map-tables.json"));
var catalog = MapCatalog.Load(maps);
var device = new GraphicsDevice();
Texture2D Read(string name, int width, int height)
{
    var bytes = File.ReadAllBytes(Path.Combine(directory, name));
    var texture = new Texture2D(device, width, height);
    for (var i = 0; i < texture.Pixels.Length; i++) texture.Pixels[i] = new(bytes[i*4],bytes[i*4+1],bytes[i*4+2],bytes[i*4+3]);
    return texture;
}
var terrain = Read("terrain.rgba", 64, 64);
var meadow = Read("meadow.rgba", 64, 64);
var town = Read("town.rgba", 144, 144);
var desertSource = Read("desert-source.rgba", 1254, 1254);
var sand = new Texture2D(device, 64, 64);
// Match RetroArt.Load's native import, including average-before-quantization.
for (var y = 0; y < 64; y++)
    for (var x = 0; x < 64; x++)
    {
        var r=0; var g=0; var b=0; var n=0;
        for (var sy=y*desertSource.Height/64; sy<(y+1)*desertSource.Height/64; sy++)
            for (var sx=x*desertSource.Width/64; sx<(x+1)*desertSource.Width/64; sx++)
            { var c=desertSource.Pixels[sy*desertSource.Width+sx]; if(c.A<128)continue; r+=c.R; g+=c.G; b+=c.B; n++; }
        r/=n;g/=n;b/=n;
        sand.Pixels[y*64+x]=RetroArt.Palette.MinBy(c=>(c.R-r)*(c.R-r)+(c.G-g)*(c.G-g)+(c.B-b)*(c.B-b));
    }
var renderer = new MountainTerrain(device, terrain, catalog, sand);
var previous = new PreviousMountainTerrain(device, terrain, catalog, sand);
var rendered = new Dictionary<int,Color[]>();
var checks=0;
void Check(bool ok, string name){ if(!ok) throw new Exception(name); checks++; Console.WriteLine("PASS "+name); }
static void Write(string path, Color[] pixels)
{
    using var file=File.Create(path);
    foreach(var c in pixels) { file.WriteByte(c.R);file.WriteByte(c.G);file.WriteByte(c.B);file.WriteByte(c.A); }
}
foreach(var id in Enumerable.Range(1,36))
{
    var map = catalog.CreateMap(id)!;
    var snapshot = Enumerable.Range(0,156).Select(i=>map.Tile(new(i/12,i%12))).ToArray();
    var clock=System.Diagnostics.Stopwatch.StartNew(); renderer.Prepare(id,map.Tile); clock.Stop();
    var pixels=renderer.Texture.Pixels; rendered[id]=pixels;
    Check(pixels.All(p=>p.A==0 || p.A==255 && RetroArt.Palette.Contains(p)), $"{id} palette and binary alpha");
    Check(Enumerable.Range(0,pixels.Length).All(i=>MountainTerrain.IsMountain(map.Tile(new(i%416/32,i/416/32))) || pixels[i].A==0),$"{id} open cells untouched");
    Check(Enumerable.Range(0,156).All(i=>!MountainTerrain.IsMountain(snapshot[i]) || Enumerable.Range(12,8).All(y=>Enumerable.Range(12,8).All(x=>pixels[(i%12*32+y)*416+i/12*32+x].A==255))),$"{id} solid blocked cores");
    Check(snapshot.SequenceEqual(Enumerable.Range(0,156).Select(i=>map.Tile(new(i/12,i%12)))), $"{id} map unchanged");
    renderer.Prepare(id,map.Tile);Check(pixels.SequenceEqual(renderer.Texture.Pixels),$"{id} deterministic cache");
    var session = new GameSession(catalog, location: new()
    { MapId=id, RegionX=(id-1)%6-4, RegionY=2-(id-1)/6, Position=new(1,1) });
    var beforeSave=GameSaveCodec.Encode(session,catalog);
    renderer.Prepare(id,session.CurrentMap.Tile);
    Check(beforeSave.SequenceEqual(GameSaveCodec.Encode(session,catalog)),$"{id} complete save and RNG unchanged");
    renderer.Prepare(id,map.Tile);
    Console.WriteLine($"{id} preparation {clock.Elapsed.TotalMilliseconds:F1}ms");
    Write(Path.Combine(directory,$"rock-{id:D3}.rgba"),pixels);
    if(!File.Exists(Path.Combine(directory,$"base-{id:D3}.rgba")))continue;
    var basis=Read($"base-{id:D3}.rgba",416,384).Pixels;
    previous.Prepare(id,map.Tile);
    for(var y=0;y<384;y++) for(var x=0;x<416;x++)
    {
        var tx=x/32;var ty=y/32;var tile=map.Tile(new(tx,ty));
        if(!MountainTerrain.IsMountain(tile))continue;
        var index=y*416+x;var variant=(tx*3+ty*7)%4;
        var floor=meadow.Pixels[(variant/2*32+y%32)*64+variant%2*32+x%32];
        var old=previous.Texture.Pixels[index];
        // Preserve foreground art from the real baseline render. Re-overlay cave
        // sprites below because their slate pixels can equal old terrain pixels.
        if(basis[index]!=(old.A==0?floor:old))continue;
        basis[index]=pixels[index].A==0?floor:pixels[index];
    }
    for(var ty=0;ty<12;ty++)for(var tx=0;tx<13;tx++)
    {
        var tile=map.Tile(new(tx,ty));
        if(tile is <0x65 or >0x68)continue;
        var vertical=tile is 0x65 or 0x67; var farEdge=tile is 0x65 or 0x66;
        var neighbor=new MapPosition(tx+(vertical?(farEdge?1:-1):0),ty+(vertical?0:(farEdge?1:-1)));
        if(!Map.Contains(neighbor)||!MountainTerrain.IsMountain(map.Tile(neighbor)))continue;
        var opaque=true;
        for(var along=0;along<32;along++)
        {
            var phase=along*MathF.Tau/32;
            var depth=5+(int)MathF.Round(2*MathF.Sin(phase)+MathF.Sin(phase*3));
            for(var d=0;d<depth;d++)
            {
                var across=farEdge?32-depth+d:d;
                var sourceAcross=farEdge?depth-1-d:31-d;
                var sx=neighbor.X*32+(vertical?sourceAcross:along);
                var sy=neighbor.Y*32+(vertical?along:sourceAcross);
                var c=pixels[sy*416+sx]; if(along is >=12 and <20)opaque &= c.A==255;
                var px=vertical?across:along;var py=vertical?along:across;
                // Transparent cliff corners expose the existing desert pattern.
                basis[(ty*32+py)*416+tx*32+px]=c.A!=0?c:sand.Pixels[py*64+(tx+ty)%2*32+px];
            }
        }
        Check(opaque,$"{id} foothill {tx},{ty} joins the solid middle of its neighboring rock");
    }
    for(var ty=0;ty<12;ty++)for(var tx=0;tx<13;tx++)
        if(map.Tile(new(tx,ty))==0x39)
            for(var y=0;y<48;y++)for(var x=0;x<48;x++)
            { var c=town.Pixels[(96+y)*144+x];var px=tx*32-8+x;var py=ty*32-12+y;
              if(c.A>0&&px>=0&&px<416&&py>=0&&py<384)basis[py*416+px]=c; }
    Write(Path.Combine(directory,$"preview-{id:D3}.rgba"),basis);
}
const int width=416;
var palette=RetroArt.Palette;
var worldMaps=Enumerable.Range(1,36).ToDictionary(id=>id,id=>catalog.CreateMap(id)!);
Color[] Prepare(Map map){renderer.Prepare(map.Id,map.Tile);return renderer.Texture.Pixels;}
static bool Rock(ushort tile)=>MountainTerrain.IsMountain(tile);
        var comparisons = 0; var matched = true;
        foreach (var (id, map) in worldMaps)
            foreach (var horizontal in new[] { true, false })
            {
                var column = (id - 1) % 6; var row = (id - 1) / 6;
                var nextId = horizontal ? row * 6 + (column + 1) % 6 + 1 : (row + 1) % 6 * 6 + column + 1;
                var next = worldMaps[nextId];
                var span = horizontal ? Map.Height : Map.Width;
                var stride = horizontal ? Map.Width - 2 : Map.Height - 2;
                MapPosition Cell(int across, int along) => horizontal ? new(across, along) : new(along, across);
                for (var along = 1; along < span - 1; along++)
                {
                    // Authored overlap cells must agree before their artwork can agree.
                    if (!Enumerable.Range(along - 1, 3).All(a => Enumerable.Range(0, 2)
                        .All(b => map.Tile(Cell(stride + b, a)) == next.Tile(Cell(b, a))))) continue;
                    for (var across = 0; across < 2; across++)
                    {
                        if (!Rock(map.Tile(Cell(stride + across, along)))) continue;
                        for (var py = 0; py < 32; py++)
                            for (var px = 0; px < 32; px++)
                            {
                                var from = Cell((stride + across) * 32 + px, along * 32 + py);
                                var to = Cell(across * 32 + px, along * 32 + py);
                                matched &= rendered[id][from.Y * width + from.X] == rendered[nextId][to.Y * width + to.X];
                                comparisons++;
                            }
                    }
                }
            }
        Check(comparisons > 10000 && matched,
            $"All {comparisons} matching mountain overlap pixels join across region edges and world wrap");

        var diagonal = rendered[18];
        var ridgeEdges = Enumerable.Range(192, 32).Select(y => Enumerable.Range(64, 160).First(x =>
            diagonal[y * width + x] == palette[2] || diagonal[y * width + x] == palette[3]
            || diagonal[y * width + x] == palette[4] || diagonal[y * width + x] == palette[39])).ToArray();
        Check(ridgeEdges.Distinct().Count() >= 12 && ridgeEdges.Max() - ridgeEdges.Min() >= 24,
            "Surface F3 beside 2,6 has a long diagonal contour instead of a lightly rounded square step");

        var fixture = new Map(17, 0, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        for (var y = 2; y <= 8; y++)
            for (var x = 3; x <= 6; x++) fixture.RestoreTile(new(x, y), 3);
        var block = Prepare(fixture);
        double Light(Color c) => c.R * .2126 + c.G * .7152 + c.B * .0722;
        var topLight = (from y in Enumerable.Range(240, 8) from x in Enumerable.Range(144, 48)
            select Light(block[y * width + x])).Average();
        var faceLight = (from y in Enumerable.Range(274, 7) from x in Enumerable.Range(144, 48)
            select Light(block[y * width + x])).Average();
        Check(topLight - faceLight >= 18, "Mountain tops remain visibly lighter than the broad front cliff faces");
        Check(block.All(p => p.A == 0 || p != palette[0] && p != palette[1]),
            "Exposed mountain rock and ground contacts never acquire an artificial near-black outline");
        var contacts = (from y in Enumerable.Range(9 * 32 - 7, 7)
            from x in Enumerable.Range(4 * 32, 2 * 32) select y * width + x).ToArray();
        Check(contacts.Any(i => block[i] == palette[11]) && contacts.Any(i => block[i] == palette[10])
            && contacts.Any(i => block[i] == palette[3] || block[i] == palette[39]),
            "Meadow grass and moss overlap broken parts of the rock foot, leaving visible stone between them");
        Check(Enumerable.Range(3 * 32, 5 * 32).All(y => Enumerable.Range(4 * 32 - 4, 8)
            .All(x => block[y * width + x].A == 255 && block[y * width + x] != palette[1])),
            "Adjacent mountain tiles form continuous rock without internal outlines or transparent seams");
        var leftEdges = Enumerable.Range(3 * 32, 5 * 32).Select(y => Enumerable.Range(3 * 32, 16)
            .First(x => block[y * width + x].A == 255)).Distinct().Count();
        Check(leftEdges >= 3 && block[2 * 32 * width + 3 * 32].A == 0,
            "Exposed mountain borders have uneven pixel ledges and cut-back corners");
        fixture.RestoreTile(new(4, 4), 0x45);
        Check(block.SequenceEqual(Prepare(fixture)), "Secret mountain passages do not change contours, texture or decoration");
        fixture.RestoreTile(new(4, 4), 2);
        Check(Prepare(fixture)[144 * width + 144].A == 0, "Removing mountain terrain invalidates its cached silhouette");
        fixture.RestoreTile(new(4, 4), 3);
        Check(block.SequenceEqual(Prepare(fixture)), "Restoring terrain reconstructs the same mountain without cache residue");

        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (!Rock(fixture.Tile(new(x, y)))) fixture.RestoreTile(new(x, y), 0x2d);
        var dry = Prepare(fixture);
        Check(dry[2 * 32 * width + 3 * 32].A == 255 && dry[2 * 32 * width + 3 * 32].R > dry[2 * 32 * width + 3 * 32].B,
            "Desert cut-back mountain corners expose warm sand instead of green meadow");
        Check(contacts.Where(i => block[i] == palette[11]).All(i => dry[i].R > dry[i].G && dry[i].G > dry[i].B)
            && contacts.All(i => dry[i] != palette[10] && dry[i] != palette[11]),
            "Desert rock feet use overlapping sand instead of meadow-green patches");
        for (var y = 0; y < Map.Height; y++)
            for (var x = 0; x < Map.Width; x++)
                if (!Rock(fixture.Tile(new(x, y)))) fixture.RestoreTile(new(x, y), 8);
        var paved = Prepare(fixture);
        Check(contacts.All(i => paved[i] != palette[10] && paved[i] != palette[11]),
            "Rock beside a paved approach does not grow a meadow fringe across it");
        var skirt = new Map(17, 0, Enumerable.Repeat((ushort)2, Map.TileCount).ToArray());
        skirt.RestoreTile(new(4, 4), 3); skirt.RestoreTile(new(5, 4), 0x67);
        var feet = Prepare(skirt);
        Check(Enumerable.Range(138, 12).All(y => feet[y * width + 159].A == 255 && feet[y * width + 159] != palette[1])
            && skirt.Move(new(5, 5), 0, -1).Moved, "Mountain rock meets its authored walkable foothill without a grass seam");


Console.WriteLine($"Passed {checks} CPU terrain checks. Previews composite cached terrain with captured foregrounds; they are not fresh GPU captures.");
