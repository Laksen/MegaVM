// See https://aka.ms/new-console-template for more information

MegaLisp.Engine engine = new();
engine.Eval("(* 3 (+ 1 2))");
engine.Eval("(cons 3 2)");
engine.Eval("(defun test (x y) (+ x y))"); 