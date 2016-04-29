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
                d "f 42 13" (appId "f" [Int 42; Int 13])
                d "(f 42 13)" (appId "f" [Int 42; Int 13])
                d "(f (g 42) 13)" (App(Var(Id "f"), [appId "g" [Int 42]; Int 13]))
                d "let x = 7 in x" (Let(((Id "x"), Type.Int), Int 7, Var(Id "x")))
                d "true" (Bool true)
                d "false" (Bool false)
                d "if true then 1 else 2" (If(Bool true, Int 1, Int 2))
                d "if (f 42) then 1 else 2" (If((appId "f" [Int 42]), Int 1, Int 2))
            |]

    let testParseOk (s:string) (a:Syntax.t<Unit>) =
        match MinFSharp.Parser.parse s with
        | Ok(ast,_) -> ast |> shouldEqual a
        | Bad(e) -> failwith (e.ToString())

    [<TestCaseSource(typeof<TCS>, "Data")>]
    let ``parse int`` (s:string,a:Syntax.t<Unit>) =
        testParseOk s a