// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open Silk.NET.Input
open Silk.NET.Maths
open Videre

type MassObject(position:Vector2D<float>, velocity:Vector2D<float>, mass:float) =(* {mutable position:Vector2D<'a>; mutable velocity:Vector2D<'a>; mass:'a}*)
    member val position = position with get, set
    member val velocity = velocity with get, set
    member val mass = mass with get

let fa(objects:List<MassObject>, i:int, q:Vector2D<float>) =
    (objects
    |> Seq.indexed
    |> Seq.filter (fun (j, _) -> j <> i)
    |> Seq.map (fun (_, x) ->
            let diff = x.position - q
            let diffL = diff.Length
            (diff*x.mass)/(diffL * diffL * diffL))
    |> Seq.sum) * 6.67408e-11

let step(objects:List<MassObject>, newObjects:List<MassObject>, i:int, h:float) =
    let o =  objects.Item(i)
    let kv1 = o.velocity
    let ka1 = fa (objects, i, o.position)
    let kv2 = o.velocity + h*0.5*ka1
    let ka2 = fa (objects, i, o.position + h*0.5*kv1)
    let kv3 = o.velocity + h*0.5*ka2
    let ka3 = fa (objects, i, o.position + h*0.5*kv2)
    let kv4 = o.velocity + h*ka3
    let ka4 = fa (objects, i, o.position + h*kv3)
    let awsum = ((1.0/6.0)*(ka1 + 2.0*ka2 + 2.0*ka3 + ka4))
    let vwsum = ((1.0/6.0)*(kv1 + 2.0*kv2 + 2.0*kv3 + kv4))
    newObjects.[i].position <- newObjects.[i].position + h*vwsum
    newObjects.[i].velocity <- newObjects.[i].velocity + h*awsum

let stepAll(objects:List<MassObject>, steps:int, stepSize:float) =
            let mutable o = objects
            for _ in 1..steps do
                let newObjects = Seq.toList o
                for x in 0..objects.Length-1 do
                    step(o, newObjects, x, stepSize)
                o <- newObjects
            o
            
let newObjects() = [
                   MassObject(Vector2D<float>(100.0, 100.0), Vector2D<float>(0.0, 0.0), 10000.0)
                   MassObject(Vector2D<float>(100.0, 500.0), Vector2D<float>(0.0, 0.0), 100000.0)
                   MassObject(Vector2D<float>(500.0, 100.0), Vector2D<float>(0.0, 0.0), 100000.0)
                   MassObject(Vector2D<float>(500.0, 500.0), Vector2D<float>(0.0, 0.0), 1000000.0)]

[<EntryPoint>]
let main argv =
    let mutable objects = newObjects()
    use engine = new VulkanEngine(new RenderGraph())
    engine.Initialize ("NBody", 1u)
    let input = engine.Input
    input.Keyboards.[0].add_KeyDown (fun _ x _ ->
        match x with
        | Key.R -> objects <- newObjects()
        | _ -> ()
        )
    engine.add_Update (fun x ->
        if (input.Keyboards.[0].IsKeyPressed(Key.Right)) then
            objects <- stepAll (objects, 100, 100.0)
        x.UnionMany (
            objects
            |> Seq.map (fun o -> [
                Action<EngineContext>(fun (x:EngineContext) -> x.Translation(float32 o.position.X, float32 o.position.Y, fun x -> x.Circle(float32 (o.mass / 10000.0))))
                Action<EngineContext>(fun (x:EngineContext) -> x.Translation(float32 (o.position.X + o.velocity.X), float32 (o.position.Y + o.velocity.Y), fun x -> x.Circle(5f)))])
            |> Seq.concat
            |> Seq.toArray)
        )
    engine.Run()
    0