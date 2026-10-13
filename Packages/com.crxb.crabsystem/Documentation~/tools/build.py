# Compiles CrabSystem and the game's scripts outside the editor with Unity's bundled .NET SDK and DLLs, for a
# quick error check after wide changes. It reads the project's .csproj defines, so open the project in Unity once.
#
#   PROJ=<project root> UNITY_DATA=<Editor/Data folder> python3 build.py [rt ed game gameed tests]
#
# PLAYER=1 compiles without UNITY_EDITOR; DET=1 makes the output deterministic (for before/after comparisons).
# CRAB overrides where the package is; by default the embedded copy, else the PackageCache one.
# Game assemblies with their own .asmdef aren't compiled; add a target for them.
import os,re,sys,subprocess,glob
P=os.path.expanduser(os.environ.get('PROJ','.')); D=os.path.expanduser(os.environ.get('UNITY_DATA','~/mnt/Data'))
embedded=P+'/Packages/com.crxb.crabsystem'
cached=sorted(glob.glob(P+'/Library/PackageCache/com.crxb.crabsystem@*'))
CRAB=os.environ.get('CRAB') or (embedded if os.path.isdir(embedded) else cached[-1])
OUT=os.path.expanduser('~/cs-out'); os.makedirs(OUT,exist_ok=True)
SA=P+'/Library/ScriptAssemblies'
CSC=[D+'/DotNetSdk/dotnet', D+'/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll']
PLAYER=os.environ.get('PLAYER')=='1'
engine=sorted(f for f in glob.glob(D+'/Managed/UnityEngine/*.dll') if not (PLAYER and 'UnityEditor' in os.path.basename(f)))+[D+'/NetStandard/ref/2.1.0/netstandard.dll']
def defines(csproj):
    d=re.search(r'<DefineConstants>([^<]*)',open(P+'/'+csproj,errors='ignore').read()).group(1).split(';')
    d=[x for x in d if not x.startswith('UNITY_6000_4')]+['UNITY_6000_6_3','UNITY_6000_6','UNITY_6000_5_OR_NEWER','UNITY_6000_6_OR_NEWER','UNITY_6000_4_OR_NEWER','UNITY_6000_3_OR_NEWER','UNITY_6000_2_OR_NEWER','UNITY_6000_1_OR_NEWER']
    if PLAYER: d=[x for x in d if not x.startswith(('UNITY_EDITOR','ENABLE_UNITY_COLLECTIONS_CHECKS'))]
    return ';'.join(sorted(set(x for x in d if x)))
asm_dirs=set()
for r,ds,fs in os.walk(P+'/Assets'):
    if any(f.endswith(('.asmdef','.asmref')) for f in fs): asm_dirs.add(r)
def owned_by_asm(path):
    d=os.path.dirname(path)
    while d.startswith(P+'/Assets'):
        if d in asm_dirs: return True
        d=os.path.dirname(d)
    return False
def cs_in(root): return sorted(glob.glob(root+'/**/*.cs',recursive=True))
def sa(names): return [SA+'/'+n+'.dll' for n in names]
def compile(name, srcs, refs, csproj):
    rsp=OUT+'/'+name+'.rsp'
    with open(rsp,'w') as f:
        f.write('-nologo -target:library -nostdlib -noconfig -langversion:9.0 -nullable:disable -unsafe '+('-deterministic' if os.environ.get('DET')=='1' else '-deterministic-')+' -warn:0\n')
        f.write(f'-out:"{OUT}/{name}.dll"\n-define:{defines(csproj)}\n')
        for r in refs: f.write(f'-r:"{r}"\n')
        for s in srcs: f.write(f'"{s}"\n')
    res=subprocess.run(CSC+['@'+rsp],capture_output=True,text=True)
    errs=[l for l in res.stdout.splitlines() if ': error ' in l]
    print(f'== {name}: {len(srcs)} files, {len(errs)} errors')
    for l in errs[:int(os.environ.get("MAXERR","40"))]: print(l.replace(P+'/',''))
    return len(errs)==0
which=sys.argv[1:] or ['rt','ed','game','gameed']
crab_refs=sa(['Unity.InputSystem','Unity.TextMeshPro','UnityEngine.UI','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime'])
if 'rt' in which: compile('CrabSystem', cs_in(CRAB+'/Runtime'), engine+crab_refs, 'Assembly-CSharp.csproj')
if 'ed' in which: compile('CrabSystem.Editor', cs_in(CRAB+'/Editor'), engine+crab_refs+[OUT+'/CrabSystem.dll'], 'Assembly-CSharp-Editor.csproj')
allsa=[f for f in glob.glob(SA+'/*.dll') if not os.path.basename(f).startswith(('Assembly-CSharp','CrabSystem'))]
loose=[f for f in cs_in(P+'/Assets') if not owned_by_asm(f)]
game=[f for f in loose if '/Editor/' not in f]
gameed=[f for f in loose if '/Editor/' in f]
if 'game' in which: compile('Assembly-CSharp', game, engine+allsa+[OUT+'/CrabSystem.dll'], 'Assembly-CSharp.csproj')
if 'gameed' in which: compile('Assembly-CSharp-Editor', gameed, engine+allsa+[OUT+'/CrabSystem.dll',OUT+'/CrabSystem.Editor.dll',OUT+'/Assembly-CSharp.dll'], 'Assembly-CSharp-Editor.csproj')
if 'tests' in which:
    nunit=glob.glob(P+'/Library/PackageCache/com.unity.ext.nunit@*/net472/unity-custom/nunit.framework.dll')
    troot=CRAB
    compile('CrabSystem.Tests', cs_in(troot+'/Tests'), engine+crab_refs+[OUT+'/CrabSystem.dll']+nunit+glob.glob(D+'/NetStandard/compat/2.1.0/shims/netfx/*.dll')+sa(['UnityEngine.TestRunner','UnityEditor.TestRunner']), 'Assembly-CSharp-Editor.csproj')
