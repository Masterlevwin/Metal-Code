using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Metal_Code
{
    public class SingleItemGroupStyleSelector : StyleSelector
    {
        // Стиль для группы с одним элементом (плоская строка)
        public Style? SingleItemStyle { get; set; }

        // Стиль для группы с несколькими элементами (Expander)
        public Style? MultiItemStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            // item — это CollectionViewGroup, когда применяется стиль к группе
            if (item is CollectionViewGroup group)
            {
                // Если в группе 1 элемент → возвращаем "плоский" стиль
                if (group.ItemCount == 1)
                    return SingleItemStyle ?? base.SelectStyle(item, container);

                // Иначе → стиль с Expander
                return MultiItemStyle ?? base.SelectStyle(item, container);
            }
            return base.SelectStyle(item, container);
        }
    }
}