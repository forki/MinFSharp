namespace MinFSharp.Tests

module EvalTests =
    open MinFSharp
    open MinFSharp.Syntax
    open MinFSharp.Interpreter
    open MinFSharp.Identifier
    open NUnit.Framework
    open FsUnitTyped
    open Chessie.ErrorHandling

    let testEvalRes env ast expRes =
        let ev = eval env ast
        match ev with
        | Bad(e) -> failwith (e.ToString())
        | Ok(e, _) -> e |> shouldEqual expRes

    [<Test>]
    let a() =
        let ast = Int 42
        testEvalRes Env.newEnv ast ast

    [<Test>]
    let ``app add``() =
        let ast =
            App(Var(Id "add"),
                [ Int 42
                  Int 3 ])
        testEvalRes Env.newEnv ast (Int 45)

    [<Test>]
    let ``let var then return``() =
        let ast = LetIn(Syntax.Decl(Id "x", Type.Int), Int 13, Var(Id "x") |> Some)
        testEvalRes Env.newEnv ast (Int 13)

    [<Test>]
    let ``function app``() =
        let ast = App((FunDef([ Id "x", Type.Int ], Body(Var(Id "x")), Type.Var None)), [ Int 13 ])
        testEvalRes Env.newEnv ast (Int 13)

    [<Test>]
    let ``function app 2``() =
        let ast = App((FunDef([ Id "x", Type.Int; Id "y", Type.Int ], Body(Var(Id "y")), Type.Var None)), [ Int 13; Int 4 ])
        testEvalRes Env.newEnv ast (Int 4)
        
    [<Test>]
    let ``binop app +``() =
        let ast = BinOp("+", Int 3 @= Pos.zero, Int 4 @= Pos.zero)
        testEvalRes Env.newEnv ast (Int 7)
