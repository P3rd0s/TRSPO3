namespace VSIXProject5
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Shell;

    using System.Text.RegularExpressions;

    using EnvDTE;
    using EnvDTE80;
    using System.IO;
    using System.Linq;

    public class StatisticSetFunc
    {
        public string FunctionName { get; set; }
        public string KeywordCount { get; set; }
        public string LinesCount { get; set; }
        public string WithoutComments { get; set; }
    }

    public struct class_stat
    {
        public int priv, pub, prot;

        public override string ToString()
        {
            return pub.ToString() + "/" + priv.ToString() + "/" + prot.ToString();
        }
    };

    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>

        private List<StatisticSetFunc> itemsFunc;


        public ToolWindow1Control()
        {
            this.InitializeComponent();
            itemsFunc = new List<StatisticSetFunc>();
            // . . . 
            Stat.ItemsSource = itemsFunc;
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            itemsFunc.Clear();

            //Get DTE
            DTE2 dte = (DTE2)Package.GetGlobalService(typeof(DTE));
            if (dte == null) return;

            //Get Document name(path)
            Document ActiveDoc = dte.ActiveDocument;
            if (ActiveDoc == null) return;


            ProjectItem DocItem = ActiveDoc.ProjectItem;
            if (DocItem == null) return;

            FileCodeModel DocModel = DocItem.FileCodeModel;
            if (DocModel == null || DocModel.CodeElements == null) return;

            foreach (CodeElement CodeElem in DocModel.CodeElements)
            {
                if (CodeElem.Kind == vsCMElement.vsCMElementFunction)
                {

                    StatisticSetFunc SSF = ParseFunction(CodeElem, false);
                    itemsFunc.Add(SSF);
                }

                else if(CodeElem.Kind == vsCMElement.vsCMElementClass)
                {
                    CodeClass cClass = CodeElem as CodeClass;
                    CodeElements all_methods = cClass.Members;
                    foreach(CodeElement method in all_methods)
                    {
                        StatisticSetFunc SSF = ParseFunction(method, true);
                        if(SSF != null)
                            itemsFunc.Add(SSF);
                    }
                }
            }

            Stat.Items.Refresh();
        }


        private string PartSetup(string text, ref StatisticSetFunc CurSet)
        {

            Regex regex_comments = new Regex(@"(\/\/.*?(\\\r?\n.*?)*(\r?\n|$))|(\/\*(?:[\s\S]*?)\*\/)");
            Regex regex_strings = new Regex(@"""(\\(\r?\n)|\\[^\n]|[^""\n])*(""|\r?\n)|\'(\\(\r?\n)|\\[^\n]|[^\'\n])*(\'|\r?\n)");

            int index_comment = 0;
            int cur_index = 0;
            int not_found = text.Length + 1;


            

            while (cur_index < text.Length)
            {

                //try find comments
                if (regex_comments.IsMatch(text, cur_index))
                {
                    index_comment = regex_comments.Match(text, cur_index).Index;
                }
                else break;

                MatchCollection all_strings = null;
                //need analyze strings
                if (regex_strings.IsMatch(text, cur_index))
                {
                    all_strings = regex_strings.Matches(text, 0);
                }

                //comment earlier in function (it means it's not a string) and comment exist (!=-1)
                int really_comment = 1;
                if (all_strings != null)
                {
                    //check all strings
                    for (int i = 0; i < all_strings.Count; i++)
                    {
                        if (index_comment > all_strings[i].Index && index_comment < all_strings[i].Index + all_strings[i].Length)
                        {
                            really_comment = 0;
                            break;
                        }
                    }
                }


                if (really_comment == 1)
                {
                    Match match = regex_comments.Match(text, cur_index);


                    text = text.Remove(index_comment, match.Length);
                    //offset, because we need to find new comments
                    cur_index = index_comment + 1;

                }
                //offset, because we need to find new comments
                cur_index++;
            }

            

            text = regex_strings.Replace(text, "");

            //keywords count
            string pattern = "\\b((alignas)|(alignof)|(and)|(and_eq)|(asm)|(auto)|(bitand)|(bitor)|" +
                        "(bool)|(break)|(case)|(catch)|(char)|(char16_t)|(char32_t)|(class)|(compl)|" +
                        "(const)|(constexpr)|(const_cast)|(continue)|(decltype)|(default)|(delete)|" +
                        "(do)|(double)|(dynamic_cast)|(else)|(enum)|(explicit)|(export)|(extern)|" +
                        "(false)|(float)|(for)|(friend)|(goto)|(if)|(inline)|(int)|(long)|(mutable)|" +
                        "(namespace)|(new)|(noexcept)|(not)|(not_eq)|(nullptr)|(operator)|(or)|" +
                        "(or_eq)|(register)|(reinterpret_cast)|(private)|(protected)|(public)|" +
                        "(return)|(short)|(signed)|(sizeof)|(static)|(static_assert)|(static_cast)|" +
                        "(struct)|(switch)|(template)|(this)|(thread_local)|(throw)|(true)|(try)|" +
                        "(typedef)|(typeid)|(typename)|(union)|(unsigned)|(using)|(virtual)|(void)|" +
                        "(volatile)|(wchar_t)|(while)|(xor)|(xor_eq)|(override)|(final))\\b";


            Regex rgx = new Regex(pattern);
            CurSet.KeywordCount = rgx.Matches(text).Count.ToString();

            rgx = new Regex(@"(\r?\n)+?|  +?");
            text = rgx.Replace(text, "");

            int prototype_end = text.IndexOf('{');
            if (prototype_end > -1)
            {
                return text.Substring(0, prototype_end).Trim();
            }

            return "";
        }

        private StatisticSetFunc ParseFunction(CodeElement codeElement, bool isClass)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StatisticSetFunc CurSet = new StatisticSetFunc();
            string text;

            CodeFunction cFunc = codeElement as CodeFunction;
            TextPoint start = cFunc.GetStartPoint(vsCMPart.vsCMPartHeader);
            text = start.CreateEditPoint().GetText(cFunc.EndPoint);


            int lines_cnt = 0;


            Regex regex_comments = new Regex(@"(\/\/.*?(\\\r?\n.*?)*(\r?\n|$))|(\/\*(?:[\s\S]*?)\*\/)");
            Regex regex_strings = new Regex(@"""(\\(\r?\n)|\\[^\n]|[^""\n])*(""|\r?\n)|\'(\\(\r?\n)|\\[^\n]|[^\'\n])*(\'|\r?\n)");

            //lines count
            lines_cnt = 1;
            foreach (char c in text)
            {
                if (c == '\n') lines_cnt++;
            }
            CurSet.LinesCount = lines_cnt.ToString();

            CurSet.FunctionName = PartSetup(text, ref CurSet);
            if (CurSet.FunctionName == "") return null;


            /*
             * 
             * Возможно все работает правильно, поэтому в данном блоке пробная версия кода
             *
             **/
            Regex empty_lines = new Regex(@"(\r\n)(\r\n)+?{");
            text = empty_lines.Replace(text, "");
            /*
            * 
            * Возможно все работает правильно, поэтому в данном блоке пробная версия кода
            *
            **/

            int index_comment = 0;
            int cur_index = 0;
            int not_found = text.Length + 1;

            string[] separators = new string[] { "\r\n" };
            List<string> funcList = text.Split(separators, System.StringSplitOptions.None).ToList();
            bool[] need_delete = new bool[funcList.Count];

            MatchCollection all_strings = null;
            //need analyze strings
            if (regex_strings.IsMatch(text, cur_index)) //ZERO INSTEAD CUR_INDEX
            {
                all_strings = regex_strings.Matches(text, cur_index);
            }
            int last_comment_position = -1;

            Regex rgx;

            while (cur_index < text.Length)
            {

                //try find comments
                if (regex_comments.IsMatch(text, cur_index))
                {
                    index_comment = regex_comments.Match(text, cur_index).Index;
                }
                else break;


                //comment earlier in function (it means it's not a string) and comment exist (!=-1)
                int really_comment = 1;
                if (all_strings != null)
                {
                    //check all strings
                    for (int i = 0; i < all_strings.Count; i++)
                    {
                        if (index_comment > all_strings[i].Index && index_comment < all_strings[i].Index + all_strings[i].Length &&(all_strings[i].Index > last_comment_position))
                        {
                            really_comment = 0;
                            break;
                        }
                    }
                }


                if(really_comment == 1)
                {
                    Match match = regex_comments.Match(text, cur_index);

                    int begin_from = 0;
                    int lines_counter = 0;
                    for(int i = 0; i < index_comment-1; i++)
                    {
                        if (text[i] == '\r' && text[i + 1] == '\n')
                            begin_from++; //remove this line 
                    }
                    for(int i = index_comment; i < index_comment + match.Length; i++)
                    {
                        if (text[i] == '\r' && text[i + 1] == '\n')
                            lines_counter++; //count of lines we need to remove

                    }
                    //this type of comment need one more string to remove, because \r\n not included
                    if(match.Value[0] == '/' && match.Value[1] == '*')
                        lines_counter++;

                    //this lines we remove later
                    for (int i = begin_from; i < begin_from + lines_counter; i++)
                        need_delete[i] = true;

                    //offset, because we need to find new comments
                    cur_index = index_comment + match.Length - 1;
                    last_comment_position = cur_index - 1;
                    
                }
                //offset, because we need to find new comments
                cur_index++;
            }

            //remove strings with comments
            for(int i = need_delete.Length -1; i >= 0; i--)
            {
                if (need_delete[i] || funcList[i].Length == 0)
                    funcList.RemoveAt(i);
            }
            text = funcList.Aggregate((a, b) => a + "\r\n" + b);

            //no_comment lines count
            lines_cnt = 1;
            foreach (char c in text)
            {
                if (c == '\n') lines_cnt++;
            }
            CurSet.WithoutComments = lines_cnt.ToString();



            return CurSet;
        }
    }
}