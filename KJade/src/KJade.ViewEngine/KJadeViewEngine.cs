﻿using KJade.Compiler.Html;
using Nancy;
using Nancy.Responses;
using Nancy.ViewEngines;
using Nancy.ViewEngines.SuperSimpleViewEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace KJade.ViewEngine
{
    public class KJadeViewEngine : IViewEngine
    {
        private readonly SuperSimpleViewEngine engineWrapper;

        public KJadeViewEngine(SuperSimpleViewEngine engineWrapper)
        {
            this.engineWrapper = engineWrapper;
        }

        public IEnumerable<string> Extensions => new[]
        {
            "jade",
            "kjade",
            "kade",
        };

        public void Initialize(ViewEngineStartupContext viewEngineStartupContext)
        {
            //Nothing to really do here
        }

        private static readonly Regex ImportRegex = new Regex(@"@import\s(?<ViewName>\w+)", RegexOptions.Compiled);

        public Response RenderView(ViewLocationResult viewLocationResult, dynamic model, IRenderContext renderContext)
        {
            var response = new HtmlResponse();
            var html = renderContext.ViewCache.GetOrAdd(viewLocationResult, result =>
            {
                return EvaluateKJade(viewLocationResult, model, renderContext);
            });

            var renderedHtml = html;

            response.Contents = stream =>
            {
                var writer = new StreamWriter(stream);
                writer.Write(renderedHtml);
                writer.Flush();
            };

            return response;
        }

        private string ReadView(ViewLocationResult locationResult)
        {
            string content;
            using (var reader = locationResult.Contents.Invoke())
            {
                content = reader.ReadToEnd();
            }
            return content;
        }

        private string PreprocessKJade(string kjade, object model, IRenderContext renderContext)
        {
            //Recursively replace @import
            kjade = ImportRegex.Replace(kjade, m =>
            {
                var partialViewName = m.Groups["ViewName"].Value;
                var partialModel = model;
                return PreprocessKJade(ReadView(renderContext.LocateView(partialViewName, partialModel)), model, renderContext);
            });
            var jadeCompiler = new JadeHtmlCompiler();
            return jadeCompiler.ReplaceInput(kjade, model);
        }

        private string EvaluateKJade(ViewLocationResult viewLocationResult, dynamic model, IRenderContext renderContext)
        {
            string content = ReadView(viewLocationResult);

            content = PreprocessKJade(content, model, renderContext);
            
            var jadeCompiler = new JadeHtmlCompiler();
            var compiledHtml = jadeCompiler.Compile(content, model);
            return compiledHtml.Value.ToString();
        }
    }
}