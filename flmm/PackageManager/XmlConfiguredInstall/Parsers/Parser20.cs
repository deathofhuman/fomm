﻿using System;
using System.Xml;
using System.Collections.Generic;
using System.Drawing;

namespace Fomm.PackageManager.XmlConfiguredInstall.Parsers
{
  /// <summary>
  /// Parses version 2.0 mod configuration files.
  /// </summary>
  public class Parser20 : Parser10
  {
    #region Properties

    /// <seealso cref="Parser.ConfigurationFileVersion"/>
    protected override string ConfigurationFileVersion
    {
      get
      {
        return "2.0";
      }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// A simple constructor that initializes teh object with the given values.
    /// </summary>
    /// <param name="p_xmlConfig">The modules configuration file.</param>
    /// <param name="p_fomodMod">The mod whose configuration file we are parsing.</param>
    /// <param name="p_dsmSate">The state of the install.</param>
    /// <param name="p_pexParserExtension">The parser extension that provides game-specific config file parsing.</param>
    public Parser20(XmlDocument p_xmlConfig, fomod p_fomodMod, DependencyStateManager p_dsmSate,
                    ParserExtension p_pexParserExtension)
      : base(p_xmlConfig, p_fomodMod, p_dsmSate, p_pexParserExtension)
    {
    }

    #endregion

    #region Abstract Method Implementations

    /// <seealso cref="Parser.GetModDependencies()"/>
    public override CompositeDependency GetModDependencies()
    {
      var cpdDependency = new CompositeDependency(DependencyOperator.And);
      var xnlDependencies = XmlConfig.SelectNodes("/config/moduleDependencies/*");
      foreach (XmlNode xndDependency in xnlDependencies)
      {
        switch (xndDependency.Name)
        {
          case "falloutDependency":
            var verMinFalloutVersion = new Version(xndDependency.Attributes["version"].InnerText);
            cpdDependency.Dependencies.Add(new GameVersionDependency(StateManager, verMinFalloutVersion));
            break;
          case "fommDependency":
            var verMinFommVersion = new Version(xndDependency.Attributes["version"].InnerText);
            cpdDependency.Dependencies.Add(new FommDependency(StateManager, verMinFommVersion));
            break;
          case "fileDependency":
            var strDependency = xndDependency.Attributes["file"].InnerText.ToLower();
            cpdDependency.Dependencies.Add(new FileDependency(strDependency, ModFileState.Active, StateManager));
            break;
          default:
            var dpnExtensionDependency = ParserExtension.ParseDependency(xndDependency, StateManager);
            if (dpnExtensionDependency != null)
            {
              cpdDependency.Dependencies.Add(dpnExtensionDependency);
            }
            else
            {
              throw new ParserException("Invalid dependency node: " + xndDependency.Name +
                                        ". At this point the config file has been validated against the schema, so there's something wrong with the parser.");
            }
            break;
        }
      }
      return cpdDependency;
    }

    /// <seealso cref="Parser.GetConditionalFileInstallPatterns()"/>
    public override IList<ConditionalFileInstallPattern> GetConditionalFileInstallPatterns()
    {
      var xnlRequiredInstallFiles = XmlConfig.SelectNodes("/config/conditionalFileInstalls/patterns/*");
      return readConditionalFileInstallInfo(xnlRequiredInstallFiles);
    }

    #endregion

    #region Parsing Methods

    /// <summary>
    /// Reads a plugin's information from the configuration file.
    /// </summary>
    /// <param name="p_xndPlugin">The configuration file node corresponding to the plugin to read.</param>
    /// <returns>The plugin information.</returns>
    protected override PluginInfo parsePlugin(XmlNode p_xndPlugin)
    {
      var strName = p_xndPlugin.Attributes["name"].InnerText;
      var strDesc = p_xndPlugin.SelectSingleNode("description").InnerText.Trim();
      IPluginType iptType = null;
      var xndTypeDescriptor = p_xndPlugin.SelectSingleNode("typeDescriptor").FirstChild;
      switch (xndTypeDescriptor.Name)
      {
        case "type":
          iptType =
            new StaticPluginType(
              (PluginType) Enum.Parse(typeof (PluginType), xndTypeDescriptor.Attributes["name"].InnerText));
          break;
        case "dependencyType":
          var ptpDefaultType =
            (PluginType)
              Enum.Parse(typeof (PluginType),
                         xndTypeDescriptor.SelectSingleNode("defaultType").Attributes["name"].InnerText);
          iptType = new DependencyPluginType(ptpDefaultType);
          var dptDependentType = (DependencyPluginType) iptType;

          var xnlPatterns = xndTypeDescriptor.SelectNodes("patterns/*");
          foreach (XmlNode xndPattern in xnlPatterns)
          {
            var ptpType =
              (PluginType)
                Enum.Parse(typeof (PluginType), xndPattern.SelectSingleNode("type").Attributes["name"].InnerText);
            var cdpDependency = loadDependency(xndPattern.SelectSingleNode("dependencies"));
            dptDependentType.AddPattern(ptpType, cdpDependency);
          }
          break;
        default:
          throw new ParserException("Invalid plaug type descriptor node: " + xndTypeDescriptor.Name +
                                    ". At this point the config file has been validated against the schema, so there's something wrong with the parser.");
      }
      var xndImage = p_xndPlugin.SelectSingleNode("image");
      Image imgImage = null;
      if (xndImage != null)
      {
        var strImageFilePath = xndImage.Attributes["path"].InnerText;
        imgImage = Fomod.GetImage(strImageFilePath);
      }
      var pifPlugin = new PluginInfo(strName, strDesc, imgImage, iptType);

      var xnlPluginFiles = p_xndPlugin.SelectNodes("files/*");
      pifPlugin.Files.AddRange(readFileInfo(xnlPluginFiles));

      var xnlPluginFlags = p_xndPlugin.SelectNodes("conditionFlags/*");
      pifPlugin.Flags.AddRange(readFlagInfo(xnlPluginFlags));

      return pifPlugin;
    }

    /// <summary>
    /// Reads the dependency information from the given node.
    /// </summary>
    /// <param name="p_xndCompositeDependency">The node from which to load the dependency information.</param>
    /// <returns>A <see cref="CompositeDependency"/> representing the dependency described in the given node.</returns>
    protected override CompositeDependency loadDependency(XmlNode p_xndCompositeDependency)
    {
      var dopOperator =
        (DependencyOperator)
          Enum.Parse(typeof (DependencyOperator), p_xndCompositeDependency.Attributes["operator"].InnerText);
      var cpdDependency = new CompositeDependency(dopOperator);
      var xnlDependencies = p_xndCompositeDependency.ChildNodes;
      foreach (XmlNode xndDependency in xnlDependencies)
      {
        switch (xndDependency.Name)
        {
          case "dependencies":
            cpdDependency.Dependencies.Add(loadDependency(xndDependency));
            break;
          case "fileDependency":
            var strDependency = xndDependency.Attributes["file"].InnerText.ToLower();
            var mfsModState =
              (ModFileState) Enum.Parse(typeof (ModFileState), xndDependency.Attributes["state"].InnerText);
            cpdDependency.Dependencies.Add(new FileDependency(strDependency, mfsModState, StateManager));
            break;
          case "flagDependency":
            var strFlagName = xndDependency.Attributes["flag"].InnerText;
            var strValue = xndDependency.Attributes["value"].InnerText;
            cpdDependency.Dependencies.Add(new FlagDependency(strFlagName, strValue, StateManager));
            break;
          default:
            throw new ParserException("Invalid plugin dependency node: " + xndDependency.Name +
                                      ". At this point the config file has been validated against the schema, so there's something wrong with the parser.");
        }
      }
      return cpdDependency;
    }

    /// <summary>
    /// Reads the condtition flag info from the given XML nodes.
    /// </summary>
    /// <param name="p_xnlFlags">The list of XML nodes containing the condition flag info to read.</param>
    /// <returns>An ordered list of <see cref="ConditionalFlag"/>s representing the data in the given list.</returns>
    private List<ConditionalFlag> readFlagInfo(XmlNodeList p_xnlFlags)
    {
      var lstFlags = new List<ConditionalFlag>();
      foreach (XmlNode xndFlag in p_xnlFlags)
      {
        var strName = xndFlag.Attributes["name"].InnerText;
        var strValue = xndFlag.InnerXml;
        lstFlags.Add(new ConditionalFlag(strName, strValue));
      }
      return lstFlags;
    }

    /// <summary>
    /// Reads the conditional file install info from the given XML nodes.
    /// </summary>
    /// <param name="p_xnlConditionalFileInstalls">The list of XML nodes containing the conditional file
    /// install info to read.</param>
    /// <returns>An ordered list of <see cref="ConditionalFileInstallPattern"/>s representing the
    /// data in the given list.</returns>
    private IList<ConditionalFileInstallPattern> readConditionalFileInstallInfo(XmlNodeList p_xnlConditionalFileInstalls)
    {
      var lstPatterns = new List<ConditionalFileInstallPattern>();
      foreach (XmlNode xndPattern in p_xnlConditionalFileInstalls)
      {
        var cdpDependency = loadDependency(xndPattern.SelectSingleNode("dependencies"));
        IList<PluginFile> lstFiles = readFileInfo(xndPattern.SelectNodes("files/*"));
        lstPatterns.Add(new ConditionalFileInstallPattern(cdpDependency, lstFiles));
      }
      return lstPatterns;
    }

    #endregion
  }
}