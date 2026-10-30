# 🎨 WPF Fluent Dark UI Design System & Component Library

Tài liệu này tổng hợp toàn bộ triết lý thiết kế, bảng mã màu, quy chuẩn typography và **bộ mã XAML mẫu (ControlTemplates/Styles)** của phong cách Fluent Dark UI hiện đại đang áp dụng trong dự án. Khi tạo mới hoặc nâng cấp ứng dụng WPF bất kỳ, bạn chỉ cần nạp lại tài liệu này để tái sử dụng toàn bộ.

---

## 📑 Mục lục
1. [Triết lý thiết kế (Design Philosophy)](#1-triết-lý-thiết-kế)
2. [Bảng mã màu & Design Tokens (Color Palette)](#2-bảng-mã-màu--design-tokens)
3. [Quy chuẩn bố cục & Căn chỉnh (Layout & Alignment Rules)](#3-quy-chuẩn-bố-cục--căn-chỉnh)
4. [Bộ Style XAML dùng chung (Toàn cục trong App.xaml)](#4-bộ-style-xaml-dùng-chung-toàn-cục-trong-appxaml)
   - [A. Dark ScrollBar (Thanh cuộn phẳng 10px)](#a-dark-scrollbar-thanh-cuộn-phẳng-10px)
   - [B. Dark ComboBox & ComboBoxItem (Xóa bỏ dropdown trắng)](#b-dark-combobox--comboboxitem)
   - [C. Dark TabControl & TabItem (Xóa bỏ tab Win32 cổ điển)](#c-dark-tabcontrol--tabitem)
5. [Bộ Style XAML cục bộ (Thường đặt tại Window.Resources)](#5-bộ-style-xaml-cục-bộ)
   - [A. Modern Card (Khối chứa nội dung bo góc)](#a-modern-card)
   - [B. Cụm nút bấm (Primary, Secondary, Danger Buttons)](#b-cụm-nút-bấm)
   - [C. Stretched ListBoxItem (Bám sát mép phải)](#c-stretched-listboxitem)
   - [D. ToolTip Dark Theme](#d-tooltip-dark-theme)
   - [E. CheckBox hiện đại](#e-checkbox-hiện-đại)
6. [Mẫu Dialog nhập liệu chuẩn (Standard Modal Dialog Pattern)](#6-mẫu-dialog-nhập-liệu-chuẩn)
7. [Checklist kiểm tra UI trước khi xuất xưởng](#7-checklist-kiểm-tra-ui-trước-khi-xuất-xưởng)

---

## 1. Triết lý thiết kế

* **Phong cách:** Fluent Design Dark Mode (lấy cảm hứng từ Windows 11 & VS Code).
* **Đặc trưng:**
  * **Xóa bỏ triệt để các control cổ điển Win32:** WPF mặc định sẽ dùng style Windows XP/7 cho `ComboBox Popup` (nền trắng chói), `TabControl` (nền xám/trắng viền thô), và `ScrollBar` (thanh xám trắng dày cộp). Toàn bộ phải được override bằng custom template dark.
  * **Hệ thống phân cấp nền tối (Elevation/Layers):** Không dùng 1 màu đen duy nhất. Tầng sâu nhất (Window Background: `#181818`) ➔ Tầng phân chia (Card: `#222222`) ➔ Tầng tương tác (Input/Button: `#262626` - `#2D2D2D`) ➔ Tầng nổi bật (Dropdown Popup: `#1E1E1E` có DropShadow).
  * **Độ tương phản cao:** Tuyệt đối không để text mặc định không gán màu (vì WPF có thể tự kế thừa màu đen làm chìm chữ). Tiêu đề luôn là `#FFFFFF`, nhãn trường (labels) là `#D0D0D0` hoặc `#CCCCCC`, thông tin phụ là `#888888`.
  * **Căn chỉnh có nguyên tắc:** Các cụm nút thao tác trên từng dòng danh sách (Active items) phải luôn dạt đều sang mép phải (`DockPanel.Dock="Right"` hoặc `Grid Column Width="Auto"` kết hợp `HorizontalContentAlignment="Stretch"`).

---

## 2. Bảng mã màu & Design Tokens

| Thành phần | Mã màu Hex | Ý nghĩa & Vị trí sử dụng |
| :--- | :--- | :--- |
| **Window Background** | `#181818` | Nền toàn bộ cửa sổ chính |
| **Header Background** | `#1F1F1F` | Thanh tiêu đề / thanh trạng thái trên cùng |
| **Card Background** | `#222222` | Nền khối thẻ (ModernCard) nhóm các control |
| **Card Border** | `#333333` | Đường viền mảnh 1px của các Card |
| **Inner Container** | `#1A1A1A` | Nền các form con, bảng tọa độ, list item nền phụ |
| **Input Background** | `#262626` | Nền TextBox, ComboBox khi ở trạng thái nghỉ |
| **Input Border** | `#3D3D3D` / `#444444` | Viền TextBox, ComboBox khi ở trạng thái nghỉ |
| **Primary Accent** | `#0078D4` | Nút chính (Save/Apply), tab active indicator |
| **Primary Hover** | `#108CEB` | Màu nút chính khi rê chuột (Hover) |
| **Primary Pressed** | `#006CBE` | Màu nút chính khi bấm giữ (Pressed) |
| **Accent Text / Glow** | `#4CC2FF` | Text xanh Fluent, tên profile, tiêu đề card con |
| **Success / Active** | `#57D97A` / `#81C784` | Badge thành công, Administrator active, không viền |
| **Success Background** | `#1B5E20` / `#1D3B26` | Nền của các badge xanh lá cây |
| **Warning / Notice** | `#FFA726` | Chú thích khóa preset mặc định, cảnh báo |
| **Danger / Delete** | `#FF6B6B` / `#C42B1C` | Nút Xóa, cảnh báo nguy hiểm |
| **Text Primary** | `#FFFFFF` | Tiêu đề, chữ trong nút chính, giá trị input |
| **Text Labels** | `#D0D0D0` / `#CCCCCC` | Nhãn trường (X, Y, Rộng, Cao, Tên profile) |
| **Text Secondary** | `#888888` / `#9E9E9E` | Chú thích phụ, subtitle, tiến trình exe |

---

## 3. Quy chuẩn bố cục & Căn chỉnh

### A. ListBox / ListView Item Alignment
Khi đặt danh sách có nút bấm bên phải:
1. Luôn gán `HorizontalContentAlignment="Stretch"` cho `ListBoxItem`. Nếu không có thuộc tính này, item chỉ rộng bằng độ dài text, làm các nút bấm bị co giật không đều.
2. Cột trái dùng `Width="*"` chứa text với `TextTrimming="CharacterEllipsis"`.
3. Cột phải dùng `Width="Auto"` chứa các nút thao tác có kích thước cố định (`Height="26"`, `Padding="6,2"`).

### B. Độ dài Text và ToolTip
* Mọi `TextBlock` hiển thị tên file, tiêu đề cửa sổ, hoặc đường dẫn dài **phải có `TextTrimming="CharacterEllipsis"`**.
* Luôn bọc trong container có `ToolTip` hiển thị toàn bộ nội dung khi rê chuột vào để người dùng không bị mất thông tin.

---

## 4. Bộ Style XAML dùng chung (Toàn cục trong App.xaml)

Đặt toàn bộ khối mã này vào `<Application.Resources>` trong file [`App.xaml`](file:///c:/PersonalProject/WindowsSplitter/App.xaml):

```xml
<Application.Resources>
    <ResourceDictionary>

        <!-- ============================================== -->
        <!-- 1. DARK SCROLLBAR (Thanh cuộn tối giản 10px)    -->
        <!-- ============================================== -->
        <Style x:Key="ScrollThumb" TargetType="{x:Type Thumb}">
            <Setter Property="IsTabStop" Value="False" />
            <Setter Property="Focusable" Value="False" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type Thumb}">
                        <Border Background="#484848" CornerRadius="4" Margin="2">
                            <VisualStateManager.VisualStateGroups>
                                <VisualStateGroup x:Name="CommonStates">
                                    <VisualState x:Name="Normal" />
                                    <VisualState x:Name="MouseOver">
                                        <Storyboard>
                                            <ColorAnimation Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                                            To="#686868" Duration="0:0:0.1" />
                                        </Storyboard>
                                    </VisualState>
                                    <VisualState x:Name="Pressed">
                                        <Storyboard>
                                            <ColorAnimation Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)"
                                                            To="#8E8E8E" Duration="0:0:0.1" />
                                        </Storyboard>
                                    </VisualState>
                                </VisualStateGroup>
                            </VisualStateManager.VisualStateGroups>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="ScrollBarTrackButton" TargetType="{x:Type RepeatButton}">
            <Setter Property="IsTabStop" Value="False" />
            <Setter Property="Focusable" Value="False" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type RepeatButton}">
                        <Border Background="Transparent" />
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="{x:Type ScrollBar}">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderBrush" Value="Transparent" />
            <Setter Property="Width" Value="10" />
            <Style.Triggers>
                <Trigger Property="Orientation" Value="Horizontal">
                    <Setter Property="Width" Value="Auto" />
                    <Setter Property="Height" Value="10" />
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type ScrollBar}">
                                <Grid Background="Transparent">
                                    <Track x:Name="PART_Track" IsDirectionReversed="False">
                                        <Track.DecreaseRepeatButton>
                                            <RepeatButton Style="{StaticResource ScrollBarTrackButton}" Command="{x:Static ScrollBar.PageLeftCommand}" />
                                        </Track.DecreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb Style="{StaticResource ScrollThumb}" />
                                        </Track.Thumb>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Style="{StaticResource ScrollBarTrackButton}" Command="{x:Static ScrollBar.PageRightCommand}" />
                                        </Track.IncreaseRepeatButton>
                                    </Track>
                                </Grid>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Trigger>
                <Trigger Property="Orientation" Value="Vertical">
                    <Setter Property="Width" Value="10" />
                    <Setter Property="Height" Value="Auto" />
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="{x:Type ScrollBar}">
                                <Grid Background="Transparent">
                                    <Track x:Name="PART_Track" IsDirectionReversed="True">
                                        <Track.DecreaseRepeatButton>
                                            <RepeatButton Style="{StaticResource ScrollBarTrackButton}" Command="{x:Static ScrollBar.PageUpCommand}" />
                                        </Track.DecreaseRepeatButton>
                                        <Track.Thumb>
                                            <Thumb Style="{StaticResource ScrollThumb}" />
                                        </Track.Thumb>
                                        <Track.IncreaseRepeatButton>
                                            <RepeatButton Style="{StaticResource ScrollBarTrackButton}" Command="{x:Static ScrollBar.PageDownCommand}" />
                                        </Track.IncreaseRepeatButton>
                                    </Track>
                                </Grid>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Trigger>
            </Style.Triggers>
        </Style>

        <!-- ============================================== -->
        <!-- 2. DARK COMBOBOX (Khử nền trắng mặc định)      -->
        <!-- ============================================== -->
        <Style TargetType="{x:Type ComboBoxItem}">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Foreground" Value="#EEEEEE" />
            <Setter Property="Padding" Value="10,7" />
            <Setter Property="FontSize" Value="13" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type ComboBoxItem}">
                        <Border x:Name="itemBorder"
                                Background="{TemplateBinding Background}"
                                BorderThickness="0"
                                CornerRadius="4"
                                Margin="1,1"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsHighlighted" Value="True">
                                <Setter TargetName="itemBorder" Property="Background" Value="#323232" />
                                <Setter Property="Foreground" Value="#FFFFFF" />
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="itemBorder" Property="Background" Value="#0078D4" />
                                <Setter Property="Foreground" Value="#FFFFFF" />
                            </Trigger>
                            <MultiTrigger>
                                <MultiTrigger.Conditions>
                                    <Condition Property="IsSelected" Value="True" />
                                    <Condition Property="IsHighlighted" Value="True" />
                                </MultiTrigger.Conditions>
                                <Setter TargetName="itemBorder" Property="Background" Value="#108CEB" />
                                <Setter Property="Foreground" Value="#FFFFFF" />
                            </MultiTrigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style x:Key="DarkComboBoxToggle" TargetType="{x:Type ToggleButton}">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="IsTabStop" Value="False" />
            <Setter Property="Focusable" Value="False" />
            <Setter Property="ClickMode" Value="Press" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type ToggleButton}">
                        <Border x:Name="templateRoot"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="5">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="28" />
                                </Grid.ColumnDefinitions>
                                <Path x:Name="arrow"
                                      Grid.Column="1"
                                      Data="M 0 0 L 4 4 L 8 0 Z"
                                      Fill="#AAAAAA"
                                      HorizontalAlignment="Center"
                                      VerticalAlignment="Center"
                                      SnapsToDevicePixels="True" />
                            </Grid>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="templateRoot" Property="Background" Value="#333333" />
                                <Setter TargetName="templateRoot" Property="BorderBrush" Value="#0078D4" />
                                <Setter TargetName="arrow" Property="Fill" Value="#FFFFFF" />
                            </Trigger>
                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="templateRoot" Property="BorderBrush" Value="#0078D4" />
                                <Setter TargetName="arrow" Property="Fill" Value="#4CC2FF" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="{x:Type ComboBox}">
            <Setter Property="Background" Value="#262626" />
            <Setter Property="Foreground" Value="#FFFFFF" />
            <Setter Property="BorderBrush" Value="#3D3D3D" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="10,6" />
            <Setter Property="FontSize" Value="13" />
            <Setter Property="MinHeight" Value="32" />
            <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Auto" />
            <Setter Property="ScrollViewer.VerticalScrollBarVisibility" Value="Auto" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type ComboBox}">
                        <Grid x:Name="templateRoot">
                            <ToggleButton x:Name="toggleButton"
                                          Background="{TemplateBinding Background}"
                                          BorderBrush="{TemplateBinding BorderBrush}"
                                          BorderThickness="{TemplateBinding BorderThickness}"
                                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                                          Style="{StaticResource DarkComboBoxToggle}" />
                            <ContentPresenter x:Name="contentPresenter"
                                              Content="{TemplateBinding SelectionBoxItem}"
                                              ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                                              ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}"
                                              ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"
                                              HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                              VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                              Margin="{TemplateBinding Padding}"
                                              IsHitTestVisible="False" />
                            <Popup x:Name="PART_Popup"
                                   AllowsTransparency="True"
                                   IsOpen="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                                   Placement="Bottom"
                                   PopupAnimation="Slide">
                                <Border x:Name="dropDownBorder"
                                        Background="#1E1E1E"
                                        BorderBrush="#3D3D3D"
                                        BorderThickness="1"
                                        CornerRadius="6"
                                        Margin="0,4,0,0"
                                        Padding="3"
                                        MinWidth="{Binding ActualWidth, ElementName=templateRoot}"
                                        MaxHeight="{TemplateBinding MaxDropDownHeight}">
                                    <Border.Effect>
                                        <DropShadowEffect BlurRadius="12" Direction="270" ShadowDepth="4" Opacity="0.5" Color="#000000" />
                                    </Border.Effect>
                                    <ScrollViewer x:Name="DropDownScrollViewer">
                                        <ItemsPresenter x:Name="ItemsPresenter" KeyboardNavigation.DirectionalNavigation="Contained" />
                                    </ScrollViewer>
                                </Border>
                            </Popup>
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property="HasItems" Value="False">
                                <Setter TargetName="dropDownBorder" Property="Height" Value="95" />
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="templateRoot" Property="Opacity" Value="0.56" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ============================================== -->
        <!-- 3. DARK TABCONTROL & TABITEM (Flat & Accent)   -->
        <!-- ============================================== -->
        <Style TargetType="{x:Type TabItem}">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="Foreground" Value="#9E9E9E" />
            <Setter Property="FontSize" Value="13" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Padding" Value="18,10" />
            <Setter Property="Margin" Value="0,0,6,0" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type TabItem}">
                        <Border x:Name="tabBorder"
                                Background="{TemplateBinding Background}"
                                BorderThickness="0,0,0,3"
                                BorderBrush="Transparent"
                                CornerRadius="6,6,0,0"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter x:Name="contentPresenter"
                                              ContentSource="Header"
                                              HorizontalAlignment="Center"
                                              VerticalAlignment="Center"
                                              RecognizesAccessKey="True" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="tabBorder" Property="Background" Value="#252525" />
                                <Setter Property="Foreground" Value="#FFFFFF" />
                            </Trigger>
                            <Trigger Property="IsSelected" Value="True">
                                <Setter TargetName="tabBorder" Property="Background" Value="#282828" />
                                <Setter TargetName="tabBorder" Property="BorderBrush" Value="#0078D4" />
                                <Setter Property="Foreground" Value="#4CC2FF" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <Style TargetType="{x:Type TabControl}">
            <Setter Property="Background" Value="Transparent" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="{x:Type TabControl}">
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto" />
                                <RowDefinition Height="*" />
                            </Grid.RowDefinitions>
                            <!-- Tab Header Strip có dải gạch phân tách mảnh -->
                            <Border Grid.Row="0" BorderBrush="#2D2D2D" BorderThickness="0,0,0,1" Margin="0,0,0,10">
                                <TabPanel IsItemsHost="True" Background="Transparent" />
                            </Border>
                            <!-- Tab Content -->
                            <Border Grid.Row="1" Background="Transparent" BorderThickness="0">
                                <ContentPresenter ContentSource="SelectedContent" />
                            </Border>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

    </ResourceDictionary>
</Application.Resources>
```

---

## 5. Bộ Style XAML cục bộ (Thường đặt tại Window.Resources)

Thường đặt vào `<Window.Resources>` trong file [`MainWindow.xaml`](file:///c:/PersonalProject/WindowsSplitter/UI/MainWindow.xaml):

```xml
<Window.Resources>
    <!-- Modern Card Style: Khối chứa thông tin bo góc 8px -->
    <Style x:Key="ModernCard" TargetType="Border">
        <Setter Property="Background" Value="#222222" />
        <Setter Property="BorderBrush" Value="#333333" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Padding" Value="16" />
    </Style>

    <!-- Primary Accent Button: Dành cho nút Lưu, Thêm, Áp dụng chính -->
    <Style x:Key="PrimaryBtn" TargetType="Button">
        <Setter Property="Background" Value="#0078D4" />
        <Setter Property="Foreground" Value="#FFFFFF" />
        <Setter Property="FontWeight" Value="SemiBold" />
        <Setter Property="Padding" Value="14,8" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                            Background="{TemplateBinding Background}"
                            CornerRadius="5"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#108CEB" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#006CBE" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Opacity" Value="0.5" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Secondary Button: Dành cho nút phụ, Hủy, Làm mới, Khôi phục -->
    <Style x:Key="SecondaryBtn" TargetType="Button">
        <Setter Property="Background" Value="#2D2D2D" />
        <Setter Property="Foreground" Value="#EEEEEE" />
        <Setter Property="BorderBrush" Value="#444444" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="12,6" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="5"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#383838" />
                        </Trigger>
                        <Trigger Property="IsPressed" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#242424" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="border" Property="Opacity" Value="0.5" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Danger Button: Dành cho nút Xóa, Gỡ bỏ -->
    <Style x:Key="DangerBtn" TargetType="Button">
        <Setter Property="Background" Value="#332222" />
        <Setter Property="Foreground" Value="#FF6B6B" />
        <Setter Property="BorderBrush" Value="#553333" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="Padding" Value="10,4" />
        <Setter Property="Cursor" Value="Hand" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border x:Name="border"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="4"
                            Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="border" Property="Background" Value="#C42B1C" />
                            <Setter Property="Foreground" Value="#FFFFFF" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Stretched ListBoxItem: Dùng để dàn phẳng dòng và dạt nút sát phải -->
    <Style x:Key="StretchedListBoxItem" TargetType="ListBoxItem">
        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="Margin" Value="0,1" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="4">
                        <ContentPresenter />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="#222222" />
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="#2A2A2A" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- Modern CheckBox -->
    <Style x:Key="ModernCheckBox" TargetType="CheckBox">
        <Setter Property="Foreground" Value="#EEEEEE" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="Margin" Value="0,5" />
        <Setter Property="Cursor" Value="Hand" />
    </Style>
</Window.Resources>
```

---

## 6. Mẫu Dialog nhập liệu chuẩn (Standard Modal Dialog Pattern)

Áp dụng mẫu này khi tạo các cửa sổ nhập liệu nhỏ (Modal/Popup Dialog) như [`SaveProfileDialog.xaml`](file:///c:/PersonalProject/WindowsSplitter/UI/SaveProfileDialog.xaml) hoặc [`AddAppDialog.xaml`](file:///c:/PersonalProject/WindowsSplitter/UI/AddAppDialog.xaml):

### Các nguyên tắc vàng cho Dialog WPF:
1. **Luôn có ScrollViewer:** Đặt `ScrollViewer` ở phần thân với `VerticalScrollBarVisibility="Auto"` và `HorizontalScrollBarVisibility="Disabled"` để phòng trường hợp máy tính có tỉ lệ scale DPI cao (125%, 150%) làm tràn chiều cao.
2. **Cho phép co giãn kích thước:** Đặt `ResizeMode="CanResizeWithGrip"` kèm `MinHeight` và `MinWidth` thay vì khóa cứng `NoResize`.
3. **Cụm nút Footer cố định:** Đặt nút Hủy và Lưu ở hàng cuối (`RowDefinition Height="Auto"`) ngoài `ScrollViewer` để các nút luôn nhìn thấy được mà không bị trôi khi cuộn.

```xml
<Window x:Class="YourApp.UI.SampleDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Tiêu đề Hộp thoại"
        Width="480"
        Height="440"
        MinHeight="380"
        MinWidth="420"
        WindowStartupLocation="CenterOwner"
        ResizeMode="CanResizeWithGrip"
        Background="#202020"
        Foreground="#FFFFFF">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0" Margin="0,0,0,14">
            <TextBlock Text="💾 Tiêu đề Dialog" FontSize="16" FontWeight="SemiBold" Foreground="#4CC2FF" />
            <TextBlock Text="Mô tả hướng dẫn ngắn gọn cho người dùng." FontSize="12" Foreground="#A0A0A0" Margin="0,4,0,0" TextWrapping="Wrap" />
        </StackPanel>

        <!-- Form Fields bọc trong ScrollViewer -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled" PanningMode="VerticalOnly">
            <StackPanel Margin="0,0,8,0">
                <StackPanel Margin="0,0,0,12">
                    <TextBlock Text="Tên trường nhập liệu *" FontSize="12" FontWeight="SemiBold" Foreground="#CCCCCC" Margin="0,0,0,4" />
                    <TextBox x:Name="TxtInput" Background="#2D2D2D" Foreground="#FFF" BorderBrush="#444" Padding="8,6" />
                </StackPanel>
            </StackPanel>
        </ScrollViewer>

        <!-- Cụm nút bấm Footer -->
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button Content="Hủy" Width="90" Padding="10,6" Background="#333333" Foreground="#CCCCCC" BorderThickness="0" IsCancel="True" Margin="0,0,10,0" />
            <Button Content="Lưu lại" Width="110" Padding="10,6" Background="#0078D4" Foreground="#FFFFFF" FontWeight="SemiBold" BorderThickness="0" IsDefault="True" />
        </StackPanel>
    </Grid>
</Window>
```

---

## 7. Checklist kiểm tra UI trước khi xuất xưởng

Khi thêm màn hình hoặc control mới, hãy kiểm tra các câu hỏi sau:

- [ ] **Màu chữ có bị chìm không?** Tất cả nhãn label (`TextBlock`) đã có `Foreground="#D0D0D0"` hoặc `#CCCCCC` chưa? (Tránh để màu mặc định khiến chữ bị đen trên nền xám tối).
- [ ] **ComboBox có bị chói mắt không?** Khi bấm mở danh sách ComboBox, dropdown xổ xuống có màu tối `#1E1E1E` với chữ trắng không?
- [ ] **Thanh cuộn có đồng bộ không?** Scrollbar đã tự động nhận style phẳng tối 10px chưa (thay vì thanh cuộn trắng dày cộp của Windows)?
- [ ] **TabControl có bị lệch tông không?** Tab đang chọn có nền tối và gạch viền xanh `#0078D4` dưới chân chưa (không bị nền trắng Win32)?
- [ ] **Các nút bấm danh sách có thẳng hàng không?** Các nút Bỏ viền, Xóa, Chi tiết đã bám sát 100% vào mép phải danh sách chưa?
- [ ] **Tên dài có bị vỡ giao diện không?** Đã bật `TextTrimming="CharacterEllipsis"` và gắn `ToolTip` để xem tên đầy đủ khi hover chưa?
- [ ] **Hộp thoại có bị tràn màn hình không?** Dialog đã có `ScrollViewer` và `ResizeMode="CanResizeWithGrip"` chưa?
