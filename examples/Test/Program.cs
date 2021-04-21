using System;
using System.Collections.Generic;
using System.Drawing;
using Silk.NET.Maths;
using Videre;

var random = new Random();
var colors = new Dictionary<int, Color>();
Color RandomColor(int i)
{
    if (colors.TryGetValue(i, out var x))
        return x;
    
    static double F(double h, double s, double v, double n)
    {
        var k = (n + h / 60) % 6;
        return v - v * s * Math.Max(Math.Min(k, Math.Min(4 - k, 1)), 0);
    }
    
    var h = random.NextDouble() * 360;
    var s = 1.0;
    var v = 1.0;
    return colors[i] = Color.FromArgb((int) (F(h, s, v, 5) * 255), (int) (F(h, s, v, 3) * 255), (int) (F(h, s, v, 1)*255));
}

using var vulkanEngine = new VulkanEngine(new RenderGraph());
vulkanEngine.Initialize("Test", 1);
vulkanEngine.Update += (ctx) =>
{
    ctx.UnionMany(
        x => x.NoneShape(), // just for formatting....
        x => x.Color(RandomColor(00), x => x.Translation(100f, 100f, x => x.Circle(30f))),
        x => x.Color(RandomColor(01), x => x.Translation(300f, 100f, x => x.Annular(5f, x => x.Circle(30f)))),
        x => x.Color(RandomColor(02), x => x.Translation(500f, 100f, x => x.Intersection(x => x.Translation(-15f, 0f, x => x.Circle(40f)), x => x.Circle(40f)))),
        x => x.Color(RandomColor(03), x => x.Translation(100f, 200f, x => x.Box(new Vector2D<float>(30f, 30f)))),
        x => x.Color(RandomColor(04), x => x.Translation(300f, 200f, x => x.Box(new Vector2D<float>(10f, 30f)))),
        x => x.Color(RandomColor(05), x => x.Translation(100f, 300f, x => x.RoundedBox(new Vector2D<float>(30f, 30f), new Vector4D<float>(5f, 5f, 5f, 5f)))),
        x => x.Color(RandomColor(06), x => x.Translation(300f, 300f, x => x.RoundedBox(new Vector2D<float>(10f, 50f), new Vector4D<float>(5f, 15f, 1f, .5f)))),
        x => x.Color(RandomColor(07), x => x.Translation(100f, 500f, x => x.Rhombus(new Vector2D<float>(30f, 30f)))),
        x => x.Color(RandomColor(08), x => x.Translation(100f, 500f, x => x.Rhombus(new Vector2D<float>(10f, 30f)))),
        x => x.Color(RandomColor(09), x => x.Translation(300f, 500f, x => x.Intersection(x => x.Translation(15f, 0f, x => x.Rhombus(new Vector2D<float>(30f, 30f))), x => x.Circle(30f)))),
        x => x.Color(RandomColor(10), x => x.Translation(500f, 500f, x => x.SmoothIntersection(5f, x => x.Translation(15f, 0f, x => x.Rhombus(new Vector2D<float>(30f, 30f))), x => x.Circle(30f)))),
        x => x.Color(RandomColor(11), x => x.Translation(700f, 500f, x => x.Annular(5f,x => x.Intersection(x => x.Translation(15f, 0f, x => x.Rhombus(new Vector2D<float>(30f, 30f))), x => x.Circle(30f))))),
        x => x.Color(RandomColor(12), x => x.Translation(900f, 500f, x => x.Annular(5f,x => x.SmoothIntersection(5f, x => x.Translation(15f, 0f, x => x.Rhombus(new Vector2D<float>(30f, 30f))), x => x.Circle(30f))))),
        x => x.Color(RandomColor(13), x => x.Translation(100f, 700f, x => x.Round(5f, x => x.Bezier(new Vector2D<float>(0, 0), new Vector2D<float>(50f, 0f), new Vector2D<float>(25f, 50f))))),
        x => x.Color(RandomColor(14), x => x.Translation(300f, 700f, x => x.Subtraction(x => x.Circle(100f), x => x.Round(5f, x => x.Bezier(new Vector2D<float>(0, 0), new Vector2D<float>(50f, 0f), new Vector2D<float>(25f, 50f))))))
        // x => x.Polygon(new Vector2D<float>(1000, 1000), new Vector2D<float>(900, 900), new Vector2D<float>(900, 1000))
    );
    // LineOfCircles(ctx, 100, 15);
    // ctx.Round(5, x => x.Segment(new Vector2D<float>(10f, 10f), new Vector2D<float>(1000f, 1000f)));
    // ctx.Translation(100f, 100f, x => x.Rhombus(new Vector2D<float>(50f, 50f)));
    // ctx.Round(10f, ctx => ctx.Bezier(new Vector2D<float>(100f, 100f), new Vector2D<float>(1000f, 100f), new Vector2D<float>(500f, 300f)));*/
};
vulkanEngine.Run();