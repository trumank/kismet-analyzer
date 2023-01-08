# KismetAnalyzer
Tools for analyzing and manipulating kismet bytecode in cooked Unreal Engine
assets.

## Example CFG generation
Assuming [graphviz](https://graphviz.org/) is in the `PATH`:
```console
kismet-analyzer gen-cfg-tree unpacked-fsd/ output/ FSD --render --progress
```
`output/` should now contain generated CFG SVGs as well as a basic web viewer to
enhance navigation of very large SVGs.
