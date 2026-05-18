from sqlalchemy import create_engine, MetaData, Table, Column, inspect
from sqlalchemy.exc import SQLAlchemyError


class DatabaseManager:
    def __init__(self, connection_string):
        """初始化数据库连接管理器"""
        self.connection_string = connection_string
        self.engine = None
        self.metadata = None
        self.inspector = None
        
    def connect(self):
        """连接到数据库"""
        try:
            self.engine = create_engine(self.connection_string)
            self.metadata = MetaData()
            self.metadata.reflect(bind=self.engine)
            self.inspector = inspect(self.engine)
            return True
        except Exception as e:
            print(f"数据库连接失败: {str(e)}")
            return False
            
    def disconnect(self):
        """断开数据库连接"""
        if self.engine:
            self.engine.dispose()
            
    def get_all_tables(self):
        """获取所有表名"""
        try:
            return self.inspector.get_table_names()
        except SQLAlchemyError as e:
            return f"获取表列表失败: {str(e)}"
            
    def get_table_schema(self, table_name):
        """获取指定表的结构信息"""
        try:
            if table_name not in self.metadata.tables:
                return f"表 '{table_name}' 不存在"
                
            columns = []
            for column in self.inspector.get_columns(table_name):
                columns.append({
                    "name": column['name'],
                    "type": str(column['type']),
                    "nullable": column['nullable'],
                    "default": str(column['default']) if column['default'] else None
                })
                
            # 获取主键
            primary_keys = self.inspector.get_pk_constraint(table_name)
            
            # 获取外键
            foreign_keys = []
            for fk in self.inspector.get_foreign_keys(table_name):
                foreign_keys.append({
                    "name": fk['name'],
                    "referred_table": fk['referred_table'],
                    "referred_columns": fk['referred_columns'],
                    "constrained_columns": fk['constrained_columns']
                })
                
            # 获取索引
            indices = []
            for index in self.inspector.get_indexes(table_name):
                indices.append({
                    "name": index['name'],
                    "columns": index['column_names'],
                    "unique": index['unique']
                })
                
            return {
                "table_name": table_name,
                "columns": columns,
                "primary_key": primary_keys,
                "foreign_keys": foreign_keys,
                "indices": indices
            }
        except SQLAlchemyError as e:
            return f"获取表 '{table_name}' 结构失败: {str(e)}"
            
    def execute_query(self, query, params=None):
        """执行自定义查询"""
        try:
            with self.engine.connect() as connection:
                if params:
                    result = connection.execute(query, params)
                else:
                    result = connection.execute(query)
                    
                # 检查是否是SELECT查询
                if result.returns_rows:
                    columns = result.keys()
                    rows = [dict(zip(columns, row)) for row in result]
                    return {"columns": columns, "rows": rows}
                else:
                    return {"affected_rows": result.rowcount}
        except SQLAlchemyError as e:
            return f"执行查询失败: {str(e)}"