﻿namespace MinFSharp

module Typing =
    open Chessie.ErrorHandling
    open Chessie.ErrorHandling.Trial

    type TypeMismatch = {expected:Type.t; actual: Type.t}

    type TypingErrorType =
        | UnknownError
        | UnknownSymbol of Identifier.t
        | TypeMismatch of TypeMismatch
    type TypingError = Syntax.Pos * TypingErrorType

    let typeMismatchAt (exp:Type.t) (act:Type.t) (p:Syntax.Pos) =
            p, TypingErrorType.TypeMismatch {expected = exp; actual = act}
    let typeMismatch (exp:Type.t) (act:Type.t) =
            Syntax.Pos.zero, TypingErrorType.TypeMismatch {expected = exp; actual = act}

    type TypingResult = Result<Type.t, TypingError>

    type TypedAstResult = Result<Type.t, TypingError>

    let rec typed (env:Env.Type ref) x : TypedAstResult =
        match x with
        | Syntax.Unit -> ok Type.Unit
        | Syntax.Bool(_) -> ok Type.Bool
        | Syntax.Int(_) -> ok Type.Int
        | Syntax.Float(_) -> ok Type.Float
        | Syntax.BinOp(op, a, b) -> typedBinOp env op a b

        | Syntax.LetIn(Syntax.Decl(vid, vty), va, insOpt) ->
            trial {
                let! tyVa = typed env va
//                match vty with
//                | Type.Var v -> ()
//                | _ -> return! fail <| typeMismatch tyVa
                let newEnv = Env.add vid tyVa !env |> ref
                match insOpt with
                | None -> return Type.Unit
                | Some ins ->
                    let! tyIns = typed newEnv ins
                    match vty with
                    | Type.Var(vt) when !vt = None ->
                        vt := Some tyVa
                        return tyIns
                    | t when t = tyVa -> return tyIns
                    | _ -> return! fail <| typeMismatch tyVa vty
            }
        | Syntax.If((posCond,cond), (posThen, ethen), (posElse, eelse)) ->
            trial {
                let! tcond = typed env cond
                if tcond <> Type.Bool then return! fail <| typeMismatch Type.Bool tcond
                let! tthen = typed env ethen
                let! telse = typed env eelse
                if tthen <> telse then return! fail <| typeMismatch tthen telse
                return tthen
            }
        | Syntax.Var(v) ->
            match (!env).tryFind v with
            | None -> fail (Syntax.Pos.zero, UnknownSymbol v)
            | Some(tyv) ->
                match tyv with
                | Type.Var V when !V = None ->
                    let nextTypeVar = Env.nextPolyType env
                    V := Some nextTypeVar
                    env := !env |> Env.add v nextTypeVar; ok (nextTypeVar)// typed env vd
                | _ -> ok (tyv)// typed env vd
        | Syntax.FunDef(args, Syntax.FBody.Ext _ext, ret) ->
            ok (Type.arrow((args |> List.map Syntax.declType) @ [ret]))
        | Syntax.FunDef(args, Syntax.FBody.Body body, ret) ->
            trial {
                let newEnv = args |> List.fold(fun e (Syntax.Decl(argId,argTy)) -> Env.add argId argTy e) !env |> ref
                let! tret = typed newEnv body
                let args = args |> List.map (fun (Syntax.Decl(argId,_argTy)) -> Syntax.Decl(argId, Env.find argId !newEnv))
                return Type.arrow((args |> List.map Syntax.declType) @ [tret])
            }
        | Syntax.App(func, args) ->
            let rec typeArrow tf args =
                trial {
                    match tf, args with
                    | Type.Fun(_, _), [] -> return tf
                    | Type.Fun(x, y), h::t when x = h -> return! typeArrow y t
                    | Type.Fun(x, _), h::_ when x <> h -> return! fail <| typeMismatch x h
                    | t, [] -> return t
                    | _ -> return! fail (Syntax.Pos.zero, UnknownError)
                }
            trial {
                let! tfunc = typed env func
                let! targs = args |> List.map (typed env) |> Trial.collect
                return! typeArrow tfunc targs
            }
        | Syntax.Seq(s) ->
            trial {
                let! ts = s |> List.map (snd >> (typed env)) |> Trial.collect
                let tss = List.zip (s |> List.map fst) ts
                return (if tss.Length = 0 then Type.Unit else List.last ts)
            }

    and typedBinOp (env) op (ap, a) (bp, b) =
        trial {
            let! tya = typed env a
            let! tyb = typed env b
            let opId = Syntax.opName op |> Identifier.Id
            match (!env).tryFind opId with
            | None -> return! fail (Syntax.Pos.zero, UnknownSymbol opId)
            | Some tyop ->
//                let! _top, tyop = typed env o
                match tyop with
                | Type.Fun(atya, Type.Fun(atyb, tret)) when atya = tya && atyb = tyb ->
                    return tret
                | Type.Fun(atya, Type.Fun(atyb, tret)) when atya <> tya || atyb <> tyb ->
                    if atya <> tya then
                        match tya with
                        | Type.Var x when !x = None ->
//                            env := Env.add
                            return tret
                        | _ -> return! fail (typeMismatch atya tya)
                    else
                        return! fail (typeMismatch atyb tyb)
                | Type.Fun(_args,_ret) -> return! fail (typeMismatchAt (Type.arrow []) (tyop) ap)
                | _ -> return! fail (typeMismatch (Type.arrow [tya; tyb; tya]) tyop)
        }
