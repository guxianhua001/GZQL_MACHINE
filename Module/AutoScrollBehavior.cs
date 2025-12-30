using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Framework
{
    public class AutoScrollBehavior : Behavior<ListView>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject.ItemsSource is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, e) =>
                {
                    if (AssociatedObject.Items.Count > 0)
                    {
                        AssociatedObject.ScrollIntoView(
                            AssociatedObject.Items[AssociatedObject.Items.Count - 1]);
                    }
                };
            }
        }
    }
}
