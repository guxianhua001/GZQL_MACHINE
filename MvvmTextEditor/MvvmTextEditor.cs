using ICSharpCode.AvalonEdit;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace MvvmTextEditor
{
    public class MvvmTextEditor : TextEditor, INotifyPropertyChanged
    {
        public MvvmTextEditor()
        {
            base.TextChanged += TextGet;

        }

        private void TextGet(object sender, EventArgs e)
        {
            Text = base.Text;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Raises a property changed event
        /// </summary>
        /// <param name="property">The name of the property that updates</param>
        public void RaisePropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        // <summary>
        /// A bindable Text property
        /// </summary>
        public new string Text
        {
            get
            {
                return (string)GetValue(TextProperty);
            }
            set
            {
                SetValue(TextProperty, value);

                RaisePropertyChanged(nameof(Text));
            }
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(MvvmTextEditor),
                new FrameworkPropertyMetadata
                {
                    DefaultValue = default(string),
                    BindsTwoWayByDefault = true,
                    PropertyChangedCallback = OnDependencyPropertyChanged
                }
            );
        /// <summary>
        /// 属性改变回调
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        protected static void OnDependencyPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            var target = (MvvmTextEditor)obj;

            if (target.Document != null)
            {
                var caretOffset = target.CaretOffset;
                var newValue = args.NewValue;

                if (newValue == null)
                {
                    newValue = "";
                }
                target.Document.Text = (string)newValue;
                target.CaretOffset = Math.Min(caretOffset, newValue.ToString().Length);
            }
        }




        //需要支持提示的DLL在此处加载
        private Assembly[] GetRelativeAssemblies()
        {
            return new Assembly[] {
                    typeof(object).Assembly, //mscorlib
                    typeof(Uri).Assembly, //System.dll
                    typeof(Enumerable).Assembly,//System.Core.dll

                };
        }

    }

}
