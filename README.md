# KismetAnalyzer
Tools for analyzing and manipulating kismet bytecode in cooked Unreal Engine
assets.

## Example CFG generation
Assuming [graphviz](https://graphviz.org/) is in the `PATH`:
```console
kismet-analyzer gen-cfg-tree unpacked-fsd/ output/ FSD --render --progress
```
`output/` should now contain generated CFGs as well as a class hierarchy.

There is also a command to generate a CFG for a single asset and open it
directly in a web browser:
```console
kismet-analyzer cfg unpacked/path/to/YourAsset.uasset
```

## Example Output

![CFG](https://github.com/trumank/kismet-analyzer/assets/1144160/a59014c1-a65c-4c1e-9f70-2589f59ca33d)
![class hierachy](https://github.com/trumank/kismet-analyzer/assets/1144160/70ad2157-6648-4140-8f12-019fb3a9cd73)
