﻿namespace MinFSharp.Tests

module ParserTests =

    open System
    open MinFSharp
    open MinFSharp.Syntax
    open MinFSharp.Interpreter
    open MinFSharp.Identifier
    open NUnit.Framework
    open FsUnitTyped
    open Chessie.ErrorHandling

    let dn name str ast  =
        TestCaseData(str, ast).SetName(name)
    let d str (ast:Syntax.t<Unit>)  =
        TestCaseData(str, ast).SetName(str)
    type TCS() =
        static member Data() =
            [|  d "42" (Int 42)
                d "(42)" (Int 42)
                d "x" (Var <| Id "x")
                d "x-1" (BinOp("-", Var <| Id "x", Int 1))
//                d "=" (Var <| Id "=")
                d "1<2" (BinOp("<", Int 1, Int 2))
                d "1 <!> 2" (BinOp("<!>", Int 1, Int 2))
                d "1 >> 2" (BinOp(">>", Int 1, Int 2))
                d "1 !@~<> 2" (BinOp("!@~<>", Int 1, Int 2))
                d "f 42 13" (appId "f" [Int 42; Int 13])
                d "(f 42 13)" (appId "f" [Int 42; Int 13])
                d "(f (g 42) 13)" (App(Var(Id "f"), [appId "g" [Int 42]; Int 13]))
                d "let x = 7 in\nx" (LetIn(((Id "x"), Type.Var None), Int 7,
                                           Some <| Var(Id "x")))
                d "let x = 1 in\nlet y = 2 in\n x+y" (LetIn(((Id "x"), Type.Var None), Int 1,
                                                            (LetIn(((Id "y"), Type.Var None), Int 2,
                                                                   BinOp("+", Var(Id "x"), Var(Id "y"))|>Some))|>Some))
                d "1;2" (Seq [Int 1; Int 2])
                d "1\n2" (Seq [Int 1; Int 2])
                d "let x : int = 7 in\nx" (LetIn(((Id "x"), Type.Int), Int 7,
                                                 Some <| Var(Id "x")))
                d "let x : bool = 7 in\nx" (LetIn(((Id "x"), Type.Bool), Int 7,
                                                 Some <| Var(Id "x")))
                d "let x : int array = () in ()" (LetIn((Id "x", Type.Array Type.Int), Unit, Some Unit))
                d "let x : int * bool = () in ()" (LetIn((Id "x", Type.Tuple [Type.Int; Type.Bool]), Unit, Some Unit))
                d "let x : int -> bool -> float = () in ()" (LetIn((Id "x", Type.Fun([Type.Int; Type.Bool], Type.Float)), Unit, Some Unit))
                d "true" (Bool true)
                d "false" (Bool false)
                d "if true then 1 else 2" (If(Bool true, Int 1, Int 2))
                d "if true then\n  1\nelse\n  2" (If(Bool true, Int 1, Int 2))
                d "if (f 42) then 1 else 2" (If((appId "f" [Int 42]), Int 1, Int 2))
                d "let min x y = if x < y then x else y" (LetIn((Id "min", Type.Var None),
                                                                (FunDef([(Id "x", Type.Var None);(Id "y", Type.Var None)],
                                                                        FBody.Body ((If (BinOp ("<",Var (Id "x"),Var (Id "y")),
                                                                                         Var (Id "x"),
                                                                                         Var (Id "y")))))),
                                                                None))
                d "let f x y = y" (LetIn((Id "f", Type.Var None), (FunDef([(Id "x", Type.Var None);(Id "y", Type.Var None)],
                                                                          FBody.Body << Var <| Id "y")),
                                         None))
                d "let fact n =\
                     if n <= 1 then 1
                     else n * (fact (n - 1))"
                   (LetIn((Id "fact", Type.Var None),
                          FunDef([(Id "n", Type.Var None)],
                                 Body(If(BinOp ("<=",Var (Id "n"),Int 1),
                                         Int 1,
                                         BinOp("*",Var (Id "n"),
                                                   App (Var (Id "fact"),[BinOp ("-", Var (Id "n"),Int 1)]))))),None))
                d "let fact n =\
                     if n <= 1 then 1
                     else n * (fact (n-1))"
                   (LetIn((Id "fact", Type.Var None),
                          FunDef([(Id "n", Type.Var None)],
                                 Body(If(BinOp ("<=",Var (Id "n"),Int 1),
                                         Int 1,
                                         BinOp("*",Var (Id "n"),
                                                   App (Var (Id "fact"),[BinOp ("-", Var (Id "n"),Int 1)]))))),None))
            |]

    let testParseOk (s:string) (a:Syntax.t<Unit>) =
        match MinFSharp.Parser.parse s with
        | Ok(ast,_) -> printf "%A" ast; ast |> shouldEqual a
        | Bad(e) -> failwith (e.ToString())

    [<TestCaseSource(typeof<TCS>, "Data")>]
    let ``parsing tests`` (s:string,a:Syntax.t<Unit>) =
        //FParsecTrace.print <- true
        testParseOk s a

    [<Test>]
    let ``fbody equality test`` () =
        let a = FunDef([(Id "n", Type.Var None)],
                                 Body(If(BinOp ("<=",Var (Id "n"),Int 1),
                                         Int 1,
                                         BinOp("*",Var (Id "n"),
                                                   App (Var (Id "fact"),[BinOp ("-", Var (Id "n"),Int -1)])))))
        a |> shouldEqual a