# MatejKafka.XmlDoc2CmdletDoc

Fork of [XmlDoc2CmdletDoc](https://github.com/red-gate/XmlDoc2CmdletDoc), retargeted to .NET 8, with support for loading
dependencies from NuGet cache for local development builds.

---

It's easy to write good help documentation for PowerShell *script* modules (those written in the PowerShell script language). You just write specially formatted comments alongside the source code for your cmdlets, and the PowerShell host automatically uses those comments to provide good inline help for your cmdlets' users. **XmlDoc2CmdletDoc** brings this same functionality to PowerShell *binary* modules (those written in C# or VB.NET). You no longer need to use *CmdletHelpEditor* or *PowerShell Cmdlet Help Editor* to manually edit a separate help file. Instead, this tool will automatically generate your PowerShell module's help file from XML Doc comments in your source code.

For more details, [Michael Sorens](https://www.simple-talk.com/author/michael-sorens/) has written a [comprehensive guide to documenting your PowerShell binary cmdlets](https://www.simple-talk.com/dotnet/software-tools/documenting-your-powershell-binary-cmdlets/) using XmlDoc2CmdletDoc.

## Usage

To create a .dll-Help.xml file for your binary PowerShell module:

1. Ensure that your project is configured to generate an XML Documentation file alongside its output assembly.
2. Install the `MatejKafka.XmlDoc2CmdletDoc` NuGet package into your project.

Alternatively, paste the following snippet into your .csproj file:

```xml
  <PropertyGroup>
    <!-- This is needed for XmlDoc2CmdletDoc to generate a PowerShell documentation file. -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="MatejKafka.XmlDoc2CmdletDoc" Version="0.4.2">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
```

Optionally, you can pass extra arguments by adding the following property to a `<PropertyGroup>`:

```xml
<XmlDoc2CmdletDocArguments>-strict -ignoreMissing -excludeParameterSets parameterSetToExclude1,parameterSetToExclude2</XmlDoc2CmdletDocArguments>
```

- `-strict`: Fail the build if any cmdlet is missing a part of the documentation.
- `-ignoreMissing`: Do not print a list of all cmdlets with missing docstrings.
- `-excludeParameterSets`: A comma-separated list of parameters sets to exclude from the documentation.

## Examples

Here are some examples of how to document your cmdlets:


### Cmdlet synopsis and description

The cmdlet's synopsis and description are defined using `<para>` elements in the cmdlet class's XML doc comment. Tag the `<para>` elements with a `type="synopsis"` or `type="description"` attribute, showing whether `<para>` is part of the synopsis or description. 

You can use multiple `<para>` elements for both the synopsis and the description, but a cmdlet synopsis is usually just one sentence.

```c#
/// <summary>
/// <para type="synopsis">This is the cmdlet synopsis.</para>
/// <para type="description">This is part of the longer cmdlet description.</para>
/// <para type="description">This is also part of the longer cmdlet description.</para>
/// </summary>
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet
{
    ...
}
```

For guidance on writing the cmdlet synopsis, see http://msdn.microsoft.com/en-us/library/bb525429.aspx.
For guidance on writing the cmdlet description, see http://msdn.microsoft.com/en-us/library/bb736332.aspx.


### Parameter description

The description for a cmdlet parameter is defined using `<para>` elements in the XML doc comment for the parameter's field or property. Tag the `<para>` elements with a `type="description"` attribute.

```c#
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet
{
    /// <summary>
    /// <para type="description">This is part of the parameter description.</para>
    /// <para type="description">This is also part of the parameter description.</para>
    /// </summary>
    [Parameter]
    public string MyParameter {get; set;}
    
    ...
}

```

For guidance on writing the parameter description, see http://msdn.microsoft.com/en-us/library/bb736339.aspx.


### Type description

You can document a parameter's input type or a cmdlet's output type, using `<para>` elements in the type's XML doc comment. As before, tag the `<para>` elements with a `type="description"` attribute. 

You can only document types defined in the PowerShell module like this.

```c#
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet
{
    [Parameter]
    public MyType MyParameter {get; set;}
    
    ...
}

/// <summary>
/// <para type="description">This is part of the type description.</para>
/// <para type="description">This is also part of the type description.</para>
/// </summary>
public class MyType
{
    ...
}
```


### Notes

You can add notes to a cmdlet's help section using a `<list>` element with a `type="alertSet"` attribute. Each `<item>` sub-element corresponds to a single note. 

Inside each `<item>` element, specify the note's title with the `<term>` sub-element, and the note's body text with the `<description>` sub-element. The `<description>` element can directly contain the note's body text, or you can split the note's body text into multiple paragraphs, using `<para>` elements.

```c#
/// <list type="alertSet">
///   <item>
///     <term>First note title</term>
///     <description>
///     This is the entire body text for the first note.
///     </description>
///   </item>
///   <item>
///     <term>Second note title</term>
///     <description>
///       <para>The first paragraph of the body text for the second note.</para>
///       <para>The second paragraph of the body text for the second note.</para>
///     </description>
///   </item>
/// </list>
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet
{
    ...
}
```

For guidance on writing cmdlet notes, see http://msdn.microsoft.com/en-us/library/bb736330.aspx.


### Examples

Cmdlet examples are defined using `<example>` elements in the XML doc comment for the cmdlet class. 

The example's code body is taken from the `<code>` element. Any `<para>` elements before the `<code>` element become the example's introduction. Any `<para>` elements  after the `<code>` element become the example's remarks. The introduction and remarks are both optional. 

To add multiple cmdlet examples, use multiple `<example>` elements.

```c#
/// <example>
///   <para>This is part of the example's introduction.</para>
///   <para>This is also part of the example's introduction.</para>
///   <code>Test-MyExample | Wrte-Host</code>
///   <para>This is part of the example's remarks.</para>
///   <para>This is also part of the example's remarks.</para>
/// </example>
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet
{
    ...
}
```

For guidance on writing cmdlet examples, see http://msdn.microsoft.com/en-us/library/bb736335.aspx.


### Related links

Related links are defined using `<para>` elements in the XML doc comment for the cmdlet class. Tag the relevant `<para>` elements with a `type="link"` attribute. The link text for each navigation link is taken from the body of the `<para>` element. If you want to include a uri, specify a uri attribute in the `<para>` element.

```c#
/// <summary>
///   <para type="link">This is the text of the first link.</para>
///   <para type="link">This is the text of the second link.</para>
///   <para type="link" uri="https://github.com/red-gate/XmlDoc2CmdletDoc/">The XmlDoc2CmdletDoc website.</para>
/// </summary>
[Cmdlet("Test", "MyExample")]
public class TestMyExampleCommand : Cmdlet
{
    ...
}
```

For guidance on writing related links, see http://msdn.microsoft.com/en-us/library/bb736334.aspx.

## Building XmlDoc2CmdletDoc

Just use `dotnet build` or `dotnet publish`.

## Contributors

- [Matej Kafka](https://github.com/MatejKafka)
- [Chris Lambrou](https://github.com/chrislambrou) (Redgate)
- [Michael Sorens](https://github.com/msorens)
- [Mirosław Rypuła](https://github.com/rymir75)
- [art-bel](https://github.com/art-bel)
- [Bryan Dunn](https://github.com/VonOgre)
- [Hamish Blake](https://github.com/hsimah)
- [lordmilko](https://github.com/lordmilko)
- [Bryan Dunn](https://github.com/VonOgre)