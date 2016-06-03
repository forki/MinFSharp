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
        let ast = Int 42 |> Lit
        testEvalRes (Env.newSymbolEnv()) ast ast

    [<Test>]
    let ``app add``() =
        let ast =
            App(pz <| Var(Id "add"),
                [ pz <| sInt 42
                  pz <| sInt 3 ])
        testEvalRes (Env.newSymbolEnv()) ast (sInt 45)

    [<Test>]
    let ``let var then return``() =
        let ast = LetIn(Syntax.Decl(Id "x", Type.Int), pz <| sInt 13, pz <| Var(Id "x") |> Some)
        testEvalRes (Env.newSymbolEnv()) ast (sInt 13)

    [<Test>]
    let ``function app``() =
        let ast = App(pz (FunDef([Syntax.Decl(Id "x", Type.Int)], Body(pz <| Var(Id "x")), Type.genType())), [ pz <| sInt 13 ])
        testEvalRes (Env.newSymbolEnv()) ast (sInt 13)

    [<Test>]
    let ``function app 2``() =
        let ast = App(pz (FunDef([Syntax.Decl(Id "x", Type.Int); Syntax.Decl(Id "y", Type.Int)], Body(pz <| Var(Id "y")), Type.genType())), [ pz <| sInt 13; pz <| sInt 4 ])
        testEvalRes (Env.newSymbolEnv()) ast (sInt 4)
        
    [<Test>]
    let ``binop app +``() =
        let ast = BinOp("+", sInt 3 @= Pos.zero, sInt 4 @= Pos.zero)
        testEvalRes (Env.newSymbolEnv()) ast (sInt 7)
