from contextlib import contextmanager
import os
import tempfile
import win32com.client
from pyautocad import APoint


class AutoCADManager:
    def __init__(self):
        """初始化 AutoCAD 连接管理器"""
        self.acad = None
        self.doc = None
        
    def connect(self):
        """连接到 AutoCAD 应用程序"""
        try:
            self.acad = win32com.client.Dispatch("AutoCAD.Application")
            # 如果 AutoCAD 未运行，尝试使其可见
            if self.acad:
                self.acad.Visible = True
                
            # 获取当前文档或创建新文档
            if self.acad.Documents.Count == 0:
                self.doc = self.acad.Documents.Add()
            else:
                self.doc = self.acad.ActiveDocument
                
            return True
        except Exception as e:
            print(f"连接 AutoCAD 失败: {str(e)}")
            return False
            
    def disconnect(self):
        """断开 AutoCAD 连接"""
        self.doc = None
        self.acad = None
        
    @contextmanager
    def autocad_session(self):
        """AutoCAD 会话的上下文管理器"""
        success = self.connect()
        if not success:
            raise Exception("无法连接到 AutoCAD")
        try:
            yield self
        finally:
            self.disconnect()
            
    def create_new_drawing(self, template=None):
        """创建新的 AutoCAD 图纸"""
        try:
            if template:
                new_doc = self.acad.Documents.Add(template)
            else:
                new_doc = self.acad.Documents.Add()
            
            self.doc = new_doc
            return "成功创建新图纸"
        except Exception as e:
            return f"创建新图纸失败: {str(e)}"
            
    def get_all_layers(self):
        """获取所有图层信息"""
        layers = []
        for i in range(self.doc.Layers.Count):
            layer = self.doc.Layers.Item(i)
            layers.append({
                "name": layer.Name,
                "color": layer.Color,
                "linetype": layer.Linetype,
                "is_locked": layer.Lock,
                "is_on": layer.LayerOn,
                "is_frozen": layer.Freeze
            })
        return layers
        
    def create_layer(self, layer_name, color=7):
        """创建新图层"""
        try:
            # 检查图层是否已存在
            try:
                existing_layer = self.doc.Layers.Item(layer_name)
                return f"图层 '{layer_name}' 已存在"
            except:
                # 图层不存在，继续创建
                new_layer = self.doc.Layers.Add(layer_name)
                new_layer.Color = color
                return f"成功创建图层 '{layer_name}'"
        except Exception as e:
            return f"创建图层失败: {str(e)}"
            
    def delete_layer(self, layer_name):
        """删除图层"""
        try:
            layer = self.doc.Layers.Item(layer_name)
            # 检查图层是否为当前图层
            if layer.Name == self.doc.ActiveLayer.Name:
                return "无法删除当前图层"
            layer.Delete()
            return f"成功删除图层 '{layer_name}'"
        except Exception as e:
            return f"删除图层失败: {str(e)}"
            
    def draw_line(self, start_x, start_y, end_x, end_y, layer=None):
        """在指定图层上绘制线条"""
        try:
            # 保存当前图层
            current_layer = self.doc.ActiveLayer.Name
            
            # 如果指定了图层，切换到该图层
            if layer:
                try:
                    self.doc.ActiveLayer = self.doc.Layers.Item(layer)
                except:
                    return f"图层 '{layer}' 不存在"
            
            # 绘制线条
            start_point = APoint(start_x, start_y)
            end_point = APoint(end_x, end_y)
            self.doc.ModelSpace.AddLine(start_point, end_point)
            
            # 恢复原图层
            if layer:
                self.doc.ActiveLayer = self.doc.Layers.Item(current_layer)
                
            return "成功绘制线条"
        except Exception as e:
            return f"绘制线条失败: {str(e)}"
            
    def export_drawing(self, file_path, file_type="DWG"):
        """导出当前图纸为指定格式"""
        try:
            # 确保文件目录存在
            os.makedirs(os.path.dirname(file_path), exist_ok=True)
            
            # 如果文件已存在，先删除
            if os.path.exists(file_path):
                os.remove(file_path)
                
            # 根据文件类型选择导出方式
            if file_type.upper() == "DWG":
                self.doc.SaveAs(file_path)
            elif file_type.upper() == "DXF":
                self.doc.Export(file_path, "DXF")
            elif file_type.upper() == "PDF":
                self.doc.Export(file_path, "PDF")
            else:
                return f"不支持的文件类型: {file_type}"
                
            return f"成功导出图纸到 {file_path}"
        except Exception as e:
            return f"导出图纸失败: {str(e)}"
            
    def get_entity_stats(self):
        """获取图纸中各类实体的统计信息"""
        entity_counts = {}
        
        # 统计模型空间中的实体
        for entity in self.doc.ModelSpace:
            entity_type = entity.ObjectName
            if entity_type in entity_counts:
                entity_counts[entity_type] += 1
            else:
                entity_counts[entity_type] = 1
                
        return entity_counts