﻿namespace MinFSharp.Tests

module TypingTests =

    open MinFSharp
    open MinFSharp.Syntax
    open MinFSharp.Interpreter
    open MinFSharp.Identifier
//    open MinFSharp.Type
    open NUnit.Framework
    open FsUnitTyped
    open Chessie.ErrorHandling

    let d ty (ast:Syntax.t) =
        TestCaseData(ast, ty, true).SetName(sprintf "%A" ast)
    let f ty (ast:Syntax.t) =
        TestCaseData(ast, ty, false).SetName(sprintf "%A" ast)
    type Tcs() =
        static member Data() =
            [| d Type.Int (Int 42)
               d (Type.Fun(Type.var "a", Type.var "a")) (Syntax.varId "id")

               d (Type.arrow[Type.Int; Type.Int; Type.Int]) (Var (Identifier.Id "(+)"))
               d (Type.arrow[Type.Int; Type.Int; Type.Int]) (Var (Identifier.Id "add"))
               d (Type.Int) (App(Var (Identifier.Id "add"), [Int 1; Int 2]))
               f (Type.Int) (App(Var (Identifier.Id "add"), [Int 1; Float 2.0]))
               f (Type.Int) (App(Var (Identifier.Id "add"), [Int 1; Int 2; Int 3]))
               d (Type.arrow[Type.Int; Type.Int]) (App (Var (Identifier.Id "add"), [Int 1]))

               d (Type.Fun(Type.var "a", Type.var "a"))
                 (Syntax.FunDef([Identifier.Id "x", Type.Var None],
                                Syntax.FBody.Body(Syntax.varId "x"),
                                Type.Var None))

               d Type.Int (BinOp("+", Int 42 @= Pos.zero, Int 42 @= Pos.zero))
               f Type.Int (BinOp("+", Int 42 @= Pos.zero, Float 42.0 @= Pos.zero))

               d Type.Int (LetIn((Id("x"),Type.Var None), (Int 3), Some <| Int 42))
               d Type.Int (LetIn((Id("x"),Type.Var None), (Int 3), Some <| varId "x"))
               d Type.Int (LetIn((Id("x"),Type.Var None), (Int 3), 
               Some <| varId "x"))
               f Type.Int (LetIn((Id("x"),Type.Unit), (Int 3), Some <| varId "x"))

               d Type.Int (If(Bool true, Int 3, Int 4))
               f Type.Int (If(Bool true, Float 4.0, Int 4))
               f Type.Int (If(Float 4.0, Int 4, Int 5))
            |]// |> Array.map d
    [<Test>]
    [<TestCaseSource(typeof<Tcs>, "Data")>]
    let ``test typing`` (ast:Syntax.t) t passes =
        match passes, Typing.typed Env.newEnv ast with
        | true, Fail(e) -> printfn "%A" e; failwith "should pass"
        | false, Fail(e) -> printfn "%A" e
        | false, Pass(ast, ty) -> printfn "res:%A\n" ast; failwith "should fail"
        | true, Pass(ast, ty) ->
            printf "%O\n" ty
            printf "%O\n" ast
            ty |> shouldEqual t
        | _, _ -> failwith "WEIRD"

