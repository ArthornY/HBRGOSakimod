using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Same verified leaf isolation as validation/beta-compat. The input profile
// remains byte-identical; all gameplay and serialization IL remains untouched.
static class TelemetryIsolation
{
    internal static string Create(string sourcePath,string reportPath)
    {
        byte[] source=File.ReadAllBytes(sourcePath);
        using var pe=new PEReader(new MemoryStream(source,writable:false));var metadata=pe.GetMetadataReader();
        var module=metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t=>metadata.GetString(t.Name)=="<Module>");
        var initializer=metadata.GetMethodDefinition(module.GetMethods().Single(h=>metadata.GetString(metadata.GetMethodDefinition(h).Name)==".cctor"));
        byte[] il=pe.GetMethodBody(initializer.RelativeVirtualAddress).GetILBytes()!;
        if(il.Length!=6||il[0]!=0x28||il[5]!=0x2a)throw new InvalidOperationException("Telemetry isolation requires exact call+ret initializer");
        var target=MetadataTokens.EntityHandle(BitConverter.ToInt32(il,1));
        if(target.Kind!=HandleKind.MethodDefinition)throw new InvalidOperationException("Telemetry call target must be a local method");
        var method=metadata.GetMethodDefinition((MethodDefinitionHandle)target);var owner=metadata.GetTypeDefinition(method.GetDeclaringType());
        if(metadata.GetString(method.Name)!="Init"||metadata.GetString(owner.Name)!="SentryAutoInit")throw new InvalidOperationException("Telemetry initializer target mismatch");
        var section=pe.PEHeaders.SectionHeaders.Single(s=>initializer.RelativeVirtualAddress>=s.VirtualAddress&&initializer.RelativeVirtualAddress<s.VirtualAddress+Math.Max(s.VirtualSize,s.SizeOfRawData));
        int body=section.PointerToRawData+initializer.RelativeVirtualAddress-section.VirtualAddress;
        int header=(source[body]&3)==2?1:(BitConverter.ToUInt16(source,body)>>12)*4;int offset=body+header;
        if(!source.AsSpan(offset,il.Length).SequenceEqual(il))throw new InvalidOperationException("Telemetry initializer PE offset mismatch");
        byte[] copy=(byte[])source.Clone();Array.Clear(copy,offset,5);
        if(!source.AsSpan(0,offset).SequenceEqual(copy.AsSpan(0,offset))||!source.AsSpan(offset+5).SequenceEqual(copy.AsSpan(offset+5)))throw new InvalidOperationException("Telemetry isolation changed bytes outside call");
        string directory=Path.Combine(Path.GetDirectoryName(reportPath)!,"telemetry-isolated");Directory.CreateDirectory(directory);string path=Path.Combine(directory,"sts2.dll");
        if(Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Refusing to overwrite telemetry source");
        File.WriteAllBytes(path,copy);
        string proof=JsonSerializer.Serialize(new{sourcePath,copyPath=path,sourceSha256=Convert.ToHexString(SHA256.HashData(source)),copySha256=Convert.ToHexString(SHA256.HashData(copy)),ilOffset=offset,originalIl=Convert.ToHexString(il),replacementIl=Convert.ToHexString(copy.AsSpan(offset,6)),outsideBytesIdentical=true});
        File.WriteAllText(Path.Combine(directory,"proof.json"),proof);
        File.WriteAllText(Path.Combine(directory,"profile-manifest.json"),JsonSerializer.Serialize(new{name="telemetry-isolated-sts2",files=new[]{new{file="sts2.dll",source=sourcePath,sourceSha256=Convert.ToHexString(SHA256.HashData(source)),copySha256=Convert.ToHexString(SHA256.HashData(copy))}},isolationProof="proof.json"},new JsonSerializerOptions{WriteIndented=true}));
        Environment.SetEnvironmentVariable("HBR_TELEMETRY_ISOLATION",proof);return path;
    }
}

