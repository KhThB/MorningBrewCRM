"""Generate the updated Vietnamese report, dictionary and editable ERD from schema.json."""
from pathlib import Path
from collections import defaultdict
import json,re,html,xml.etree.ElementTree as ET
from docx import Document
from docx.shared import Inches,Pt,RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT,WD_CELL_VERTICAL_ALIGNMENT
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
DOCS=ROOT/'docs'; DOCS.mkdir(exist_ok=True)
DATA=json.loads((DOCS/'schema.json').read_text(encoding='utf-8'))
TABLES=DATA['tables']; BYNAME={t['name']:t for t in TABLES}
CHANGE={
 'Roles':'Giữ 5 vai trò; chuyển tên nghiệp vụ thành Code ổn định và Name tiếng Việt.',
 'Users':'Dùng email đăng nhập và NormalizedEmail duy nhất; bỏ Username; mật khẩu băm, thêm Version và IsActive.',
 'Areas':'Chuẩn hóa tên và trạng thái; phục vụ phạm vi khu vực.',
 'Branches':'Thêm giờ phục vụ, công tắc nhận đơn/pickup/delivery, phí giao và Version; quản lý phải đúng vai trò.',
 'AreaManagers':'Giữ phân công N–N, thêm IsActive để ngừng phân công mà giữ lịch sử.',
 'StaffBranches':'Giữ phân công N–N; kiểm tra nhân viên chỉ lập/xử lý đơn tại nơi đang hoạt động.',
 'Categories':'Giữ danh mục và trạng thái dùng chung.',
 'Products':'Chuyển BasePrice sang ProductVariants; Products tập trung mô tả món. Thêm Version.',
 'BranchProducts':'Giữ phân phối và tình trạng bán món; chuyển giá sang BranchProductVariants theo size.',
 'Customers':'Tên/email/điện thoại chỉ ở Users; địa chỉ chuyển CustomerAddresses. Điểm cho phép âm khi thu hồi.',
 'CustomerPreferences':'Mỗi dòng là một món yêu thích; bỏ PreferenceLevel vì bản đầu dùng yêu thích có/không.',
 'Orders':'Mở rộng kênh, hình thức nhận, tổng tiền, voucher, thời hạn thanh toán và chống yêu cầu lặp; bỏ PaymentMethod để dùng PaymentAttempts.',
 'OrderDetails':'Thêm phiên bản, tên và giá lịch sử, phụ phí. Tùy chọn chi tiết chuyển OrderItemModifiers.',
 'LoyaltyTransactions':'Thêm loại thu hồi, mã chống trùng và liên kết voucher; không cập nhật/xóa bút toán cũ.',
 'Promotions':'Thêm phạm vi, điểm đổi, hạn mức phát hành, đơn tối thiểu và trần giảm phần trăm.',
 'CustomerPromotions':'Một dòng là một voucher cụ thể; thêm mã, điều kiện chụp, trạng thái giữ và đơn sử dụng.',
 'Feedbacks':'Thêm OrderID và kiểm tra quyền sở hữu/chi nhánh/món tương ứng; giữ một phản hồi chính thức.',
 'Surveys':'Thêm phạm vi quản trị; khóa cấu trúc/thời hạn sau công bố.',
 'SurveyQuestions':'Bỏ OptionsJSON, chuyển đáp án sang SurveyQuestionOptions; thêm bắt buộc và giới hạn Rating.',
 'SurveyRecipients':'CompletedAt quyết định đã hoàn thành, không lưu Status trùng ý nghĩa. Giữ khóa ghép khảo sát/khách.',
 'SurveyResponses':'Thêm SurveyID cho FK ghép; thay AnswerValue bằng TextValue/RatingValue và bảng đáp án được chọn.'}

def field_rules(t,c):
    out=['Được trống' if c['nullable'] else 'Bắt buộc']
    if c['name'] in t['pk']: out.append('PK ghép' if len(t['pk'])>1 else 'PK')
    if c['ref']: out.append('FK → '+c['ref'].replace('.', '.\n'))
    if any(c['name'] in u for u in t['uniques']): out.append('UNIQUE' if [c['name']] in t['uniques'] else 'UNIQUE ghép')
    if c['default'] is not None: out.append('Mặc định: '+c['default'])
    if 'IDENTITY' in c['type']: out.append('Tự tăng từ 1')
    if c['type']=='rowversion': out.append('SQL tự sinh')
    return '\n'.join(out)

def source(t,c):
    if c['source']!='Backend kiểm tra và ghi từ dữ liệu người dùng hoặc nghiệp vụ tương ứng.': return c['source']
    name=t['name']; col=c['name']
    if name in ('OrderDetails','OrderItemModifiers'):
        return 'CreateOrder chụp từ menu sau kiểm tra.' if col not in ('Quantity','Note') else 'Khách/nhân viên nhập lúc đặt; backend kiểm tra.'
    if name=='Orders': return 'CreateOrder tính/chụp; trạng thái và thời điểm do procedure nghiệp vụ cập nhật.'
    if name=='CustomerPromotions': return 'IssueVoucher chụp điều kiện; nghiệp vụ đơn cập nhật giữ/sử dụng.'
    if name=='LoyaltyTransactions': return 'Procedure nghiệp vụ điểm ghi một lần trong transaction.'
    if name in ('PaymentAttempts','Refunds'): return 'Procedure thanh toán/hoàn tiền ghi; kết quả hiện từ mô phỏng.'
    if name=='Deliveries': return 'Chụp khi đặt; trạng thái/người giao do POS cập nhật.'
    if name.startswith('Survey'):
        return 'Quản lý cấu hình khi Draft; khóa sau công bố.' if name in ('Surveys','SurveyQuestions','SurveyQuestionOptions') else 'Backend ghi khi mời hoặc nhận bộ câu trả lời hợp lệ.'
    if name in ('Customers','CustomerAddresses','CustomerPreferences'): return 'Khách cập nhật qua backend sau kiểm tra quyền sở hữu.'
    if name in ('Notifications','AuditLogs','OrderStatusHistory'): return 'Backend/procedure ghi khi sự kiện phát sinh.'
    return 'Quản trị/backend ghi trong phạm vi được cấp quyền.'

def relationship_list():
    edges=[]
    for t in TABLES:
        for c in t['columns']:
            if c['ref']:
                parent,key=c['ref'].split('.')
                edges.append((parent,t['name'],key,c['name'],c['nullable'],[c['name']]==t['pk'] or [c['name']] in t['uniques']))
    return edges

def drawio():
    root=ET.Element('mxfile',host='app.diagrams.net',version='26.0.0')
    diag=ET.SubElement(root,'diagram',name='ERD 41 bang',id='morningbrew-41')
    model=ET.SubElement(diag,'mxGraphModel',grid='1',gridSize='10',page='0',pageScale='1')
    rt=ET.SubElement(model,'root'); ET.SubElement(rt,'mxCell',id='0'); ET.SubElement(rt,'mxCell',id='1',parent='0')
    palette={'Tài khoản':'#e9eef5','Tổ chức':'#edf3e7','Thực đơn':'#fff1dd','CRM':'#efe9f8','Điểm và ưu đãi':'#fff8d9','Bán hàng':'#e4f1f5','Thanh toán':'#f8e9e8','Khảo sát':'#e6f1e9'}
    y=30
    for row in range(0,len(TABLES),5):
        height=max(62+len(t['columns'])*21 for t in TABLES[row:row+5])
        for k,t in enumerate(TABLES[row:row+5]):
            lines=[f'<b>{t["name"]}</b>',f'<i>{t["group"]} · {"Gốc" if t["original"] else "Mới"}</i>']
            for c in t['columns']:
                badges=('PK ' if c['name'] in t['pk'] else '')+('FK ' if c['ref'] else '')
                lines.append(html.escape(badges+c['name']+' : '+c['type'].replace(' IDENTITY(1,1)','')+(' ?' if c['nullable'] else '')))
            cell=ET.SubElement(rt,'mxCell',id=t['name'],value='<br>'.join(lines),style=f'rounded=0;whiteSpace=wrap;html=1;align=left;verticalAlign=top;spacing=9;fontFamily=Arial;fontSize=12;fillColor={palette[t["group"]]};strokeColor=#758292;',vertex='1',parent='1')
            ET.SubElement(cell,'mxGeometry',x=str(30+k*430),y=str(y),width='365',height=str(62+len(t['columns'])*21),**{'as':'geometry'})
        y+=height+110
    for i,(parent,child,key,col,optional,one) in enumerate(relationship_list()):
        style=f'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#76818e;fontSize=9;startArrow={"ERzeroToOne" if optional else "ERone"};endArrow={"ERzeroToOne" if one else "ERzeroToMany"};labelBackgroundColor=#ffffff;'
        e=ET.SubElement(rt,'mxCell',id=f'fk{i}',value=col,style=style,edge='1',parent='1',source=parent,target=child)
        ET.SubElement(e,'mxGeometry',relative='1',**{'as':'geometry'})
    ET.indent(root)
    ET.ElementTree(root).write(DOCS/'MorningBrewCRM.drawio',encoding='utf-8',xml_declaration=True)
    m=['erDiagram']
    for t in TABLES:
        m.append('    '+t['name']+' {')
        for c in t['columns']:
            typ=c['type'].split('(')[0].split()[0]
            tag=(' PK' if c['name'] in t['pk'] else '') or (' FK' if c['ref'] else '')
            m.append(f'        {typ} {c["name"]}{tag}')
        m.append('    }')
    for parent,child,key,col,optional,one in relationship_list():
        m.append(f'    {parent} {"|o" if optional else "||"}--{"o|" if one else "o{"} {child} : "{col}"')
    (DOCS/'MorningBrewCRM.mmd').write_text('\n'.join(m),encoding='utf-8')

def diagram_png(filename,names,links,title):
    # Deliberately uncluttered relationship excerpts; the drawio file carries every field and FK.
    w,h=1500,900
    im=Image.new('RGB',(w,h),'white'); d=ImageDraw.Draw(im)
    font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25); small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',21); bold=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',28)
    d.text((40,20),title,font=bold,fill='#182532')
    pos={n:(60+(i%3)*490,105+(i//3)*230) for i,n in enumerate(names)}
    for a,b in links:
        ax,ay=pos[a]; bx,by=pos[b]
        if ay==by:
            start=(ax+400,ay+65) if bx>ax else (ax,ay+65)
            end=(bx,by+65) if bx>ax else (bx+400,by+65)
        else:
            start=(ax+200,ay+135) if by>ay else (ax+200,ay)
            end=(bx+200,by) if by>ay else (bx+200,by+135)
        if ay==by and abs(ax-bx)<=490:
            route=[start,end]
        elif ay==by:
            start=(ax+200,ay); end=(bx+200,by)
            route=[start,(start[0],ay-25),(end[0],ay-25),end]
        elif abs(ay-by)<=230:
            gap=(start[1]+end[1])/2
            route=[start,(start[0],gap),(end[0],gap),end]
        else:
            lane=30 if bx<=ax else 1465
            gap1=ay+170 if by>ay else ay-30
            gap2=by-30 if by>ay else by+170
            route=[start,(start[0],gap1),(lane,gap1),(lane,gap2),(end[0],gap2),end]
        d.line(route,fill='#718197',width=3)
        ex,ey=end; sx,sy=start
        sx,sy=route[-2]
        import math
        ang=math.atan2(ey-sy,ex-sx)
        tri=[end,(ex-12*math.cos(ang-0.45),ey-12*math.sin(ang-0.45)),(ex-12*math.cos(ang+0.45),ey-12*math.sin(ang+0.45))]
        d.polygon(tri,fill='#718197')
    for n,(x,y) in pos.items():
        d.rectangle((x,y,x+400,y+135),fill='#eef2f6',outline='#8a97a6',width=2)
        d.text((x+15,y+16),n,font=font,fill='#132536')
        pk=', '.join(BYNAME[n]['pk']);
        if len(pk)>33: pk=pk[:31]+'…'
        d.text((x+15,y+62),'PK: '+pk,font=small,fill='#33445a')
        d.text((x+15,y+99),str(len(BYNAME[n]['columns']))+' trường dữ liệu',font=small,fill='#33445a')
    d.text((40,860),'Mũi tên: bảng được tham chiếu → bảng giữ khóa ngoại. ERD đầy đủ: MorningBrewCRM.drawio.',font=small,fill='#536274')
    dest=DOCS/filename; im.save(dest); return dest

def style_table(table,widths):
    table.alignment=WD_TABLE_ALIGNMENT.CENTER; table.autofit=False
    for col,width in zip(table.columns,widths): col.width=Inches(width)
    for ri,row in enumerate(table.rows):
        for ci,cell in enumerate(row.cells):
            cell.width=Inches(widths[ci]); cell.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
            pr=cell._tc.get_or_add_tcPr()
            shade=OxmlElement('w:shd'); shade.set(qn('w:fill'),'E9EEF3' if ri==0 else ('F7F9FB' if ri%2==0 else 'FFFFFF')); pr.append(shade)
            margins=OxmlElement('w:tcMar')
            for side in ('top','left','bottom','right'):
                el=OxmlElement('w:'+side); el.set(qn('w:w'),'85'); el.set(qn('w:type'),'dxa'); margins.append(el)
            pr.append(margins)
            borders=OxmlElement('w:tcBorders')
            for side in ('top','left','bottom','right'):
                el=OxmlElement('w:'+side); el.set(qn('w:val'),'single'); el.set(qn('w:sz'),'4'); el.set(qn('w:color'),'D9D9D9'); borders.append(el)
            pr.append(borders)
            for p in cell.paragraphs:
                p.paragraph_format.space_after=Pt(3); p.paragraph_format.space_before=Pt(0); p.paragraph_format.line_spacing=1.05
                p.paragraph_format.keep_with_next=(ri==0)
                for r in p.runs: r.font.size=Pt(9); r.bold=ri==0
        trPr=row._tr.get_or_add_trPr(); trPr.append(OxmlElement('w:cantSplit'))
    hdr=OxmlElement('w:tblHeader'); table.rows[0]._tr.get_or_add_trPr().append(hdr)

def doc_table(doc,headers,rows,widths):
    table=doc.add_table(rows=1,cols=len(headers))
    for cell,text in zip(table.rows[0].cells,headers): cell.text=text
    for row in rows:
        cells=table.add_row().cells
        for cell,text in zip(cells,row): cell.text=str(text)
    style_table(table,widths)
    doc.add_paragraph().paragraph_format.space_after=Pt(2)
    return table

def make():
    drawio()
    paths=[]
    diagrams=[
      ('erd-orders.png',['Customers','Orders','OrderDetails','Deliveries','PaymentAttempts','OrderItemModifiers','OrderStatusHistory','Refunds','LoyaltyTransactions'],[('Customers','Orders'),('Orders','OrderDetails'),('Orders','Deliveries'),('Orders','PaymentAttempts'),('OrderDetails','OrderItemModifiers'),('Orders','OrderStatusHistory'),('PaymentAttempts','Refunds'),('Orders','LoyaltyTransactions')],'Đơn hàng thanh toán và giao hàng'),
      ('erd-menu.png',['Categories','Products','ProductVariants','Branches','BranchProducts','BranchProductVariants','ModifierGroups','ProductModifierGroups','ModifierOptions'],[('Categories','Products'),('Products','ProductVariants'),('Branches','BranchProducts'),('Products','BranchProducts'),('ProductVariants','BranchProductVariants'),('ModifierGroups','ModifierOptions'),('Products','ProductModifierGroups'),('ModifierGroups','ProductModifierGroups')],'Thực đơn và tùy chọn món'),
      ('erd-crm.png',['Users','Customers','CustomerAddresses','Promotions','CustomerPromotions','Orders','LoyaltyPolicies','LoyaltyTransactions','Notifications'],[('Users','Customers'),('Customers','CustomerAddresses'),('Customers','CustomerPromotions'),('Promotions','CustomerPromotions'),('CustomerPromotions','Orders'),('Customers','LoyaltyTransactions'),('LoyaltyPolicies','Orders'),('Users','Notifications')],'Khách hàng điểm và voucher'),
      ('erd-survey.png',['Surveys','SurveyQuestions','SurveyQuestionOptions','Customers','SurveyRecipients','SurveyResponses','Feedbacks','SurveyAnswerOptions','Notifications'],[('Surveys','SurveyQuestions'),('SurveyQuestions','SurveyQuestionOptions'),('Surveys','SurveyRecipients'),('Customers','SurveyRecipients'),('SurveyQuestions','SurveyResponses'),('Customers','SurveyResponses'),('SurveyResponses','SurveyAnswerOptions'),('SurveyQuestionOptions','SurveyAnswerOptions'),('Customers','Feedbacks')],'Khảo sát phản hồi và thông báo')]
    for args in diagrams: paths.append(diagram_png(*args))
    doc=Document(); sec=doc.sections[0]
    sec.page_width=Inches(8.27); sec.page_height=Inches(11.69)
    sec.top_margin=sec.bottom_margin=Inches(.65); sec.left_margin=sec.right_margin=Inches(.65)
    for name in ('Normal','Title','Subtitle','Heading 1','Heading 2','Heading 3'):
        st=doc.styles[name]; st.font.name='Arial'; st.font.color.rgb=RGBColor(0,0,0)
        st.font.underline=False
        for border in st.element.xpath('.//w:pBdr'): border.getparent().remove(border)
    normal=doc.styles['Normal']; normal.font.size=Pt(10); normal.paragraph_format.space_after=Pt(6); normal.paragraph_format.line_spacing=1.12
    doc.styles['Title'].font.size=Pt(23)
    for n,size in [('Heading 1',16),('Heading 2',12),('Heading 3',11)]:
        doc.styles[n].font.size=Pt(size); doc.styles[n].paragraph_format.keep_with_next=True
    header=sec.header.paragraphs[0]; header.text='MORNING BREW COFFEE  |  THIẾT KẾ CƠ SỞ DỮ LIỆU'; header.style=doc.styles['Normal']
    for r in header.runs: r.font.size=Pt(8); r.font.color.rgb=RGBColor(80,80,80)
    footer=sec.footer.paragraphs[0]; footer.alignment=WD_ALIGN_PARAGRAPH.RIGHT
    footer.add_run('Morning Brew CRM  •  Trang ')
    fld=OxmlElement('w:fldSimple'); fld.set(qn('w:instr'),'PAGE'); footer._p.append(fld)
    title=doc.add_heading('Hệ thống quản lý chuỗi quán cà phê kết hợp CRM',0)
    border=OxmlElement('w:pBdr')
    for side in ('top','left','bottom','right'):
        edge=OxmlElement('w:'+side); edge.set(qn('w:val'),'nil'); border.append(edge)
    title._p.get_or_add_pPr().append(border)
    doc.add_paragraph('Thiết kế cơ sở dữ liệu cho web Admin web POS và app khách hàng',style='Subtitle')
    doc.add_paragraph(f'Bản cập nhật triển khai • 41 bảng • {sum(len(t["columns"]) for t in TABLES)} trường dữ liệu • SQL Server 2022 trở lên')
    doc.add_paragraph('Tài liệu giải thích phạm vi nghiệp vụ, các quan hệ và từng trường dữ liệu để dùng làm nền tảng cho đồ án. Thiết kế mở rộng 21 bảng gốc thành 41 bảng; kèm script có thể chạy, dữ liệu mẫu, ERD chỉnh sửa được và bộ kiểm thử nghiệp vụ.')
    doc.add_heading('Cách đọc tài liệu',1)
    doc.add_paragraph('Phần đầu mô tả nghiệp vụ và quy tắc. Tiếp theo là sơ đồ quan hệ, đối chiếu với Word gốc và từ điển chi tiết theo từng bảng. Phần cuối ghi các điểm cần nối với backend và kết quả kiểm thử. File README.md hướng dẫn cài đặt và chạy thử trên máy.')
    doc.add_page_break()
    arch=(DOCS/'architecture.md').read_text(encoding='utf-8')
    for line in arch.splitlines():
        if line.startswith('# '): continue
        if line.startswith('## '): doc.add_heading(line[3:],1)
        elif line.strip(): doc.add_paragraph(line)
    doc.add_page_break(); doc.add_heading('Sơ đồ quan hệ dữ liệu',1)
    doc.add_paragraph('Các hình dưới đây tóm tắt các cụm quan hệ quan trọng. File MorningBrewCRM.drawio chứa đủ 41 bảng, toàn bộ trường và các khóa ngoại đơn; ràng buộc khóa ngoại ghép được mô tả trong từng bảng và script SQL. Dấu hỏi sau kiểu dữ liệu trên ERD biểu thị trường cho phép NULL.')
    for i,path in enumerate(paths):
        if i==2: doc.add_page_break()
        doc.add_heading(diagrams[i][3],2); doc.add_picture(str(path),width=Inches(6.95))
    doc.add_page_break(); doc.add_heading('Đối chiếu với tài liệu gốc',1)
    doc.add_paragraph('Giữ tên 21 bảng để đối chiếu trực tiếp; các thay đổi dưới đây cần phản ánh vào phần chức năng và phần thiết kế dữ liệu của đồ án.')
    doc_table(doc,['Bảng gốc','Thay đổi và lý do'],[(n,CHANGE[n]) for n in CHANGE],[1.6,5.35])
    doc.add_heading('Hai mươi bảng bổ sung',1)
    doc_table(doc,['Bảng mới','Lý do cần có'],[(t['name'],t['purpose']) for t in TABLES if not t['original']],[1.65,5.3])
    doc.add_page_break(); doc.add_heading('Từ điển dữ liệu chi tiết',1)
    doc.add_paragraph('Cột Quy tắc ghi tính bắt buộc, khóa và mặc định. UNIQUE ghép nghĩa là tổ hợp các trường duy nhất, không phải từng trường riêng. Ràng buộc CHECK, FK ghép và index được liệt kê sau bảng. Ví dụ chỉ minh họa định dạng, không luôn là giá trị hiện có trong dữ liệu mẫu.')
    md=['# Từ điển dữ liệu Morning Brew CRM','',f'41 bảng, {sum(len(t["columns"]) for t in TABLES)} trường. Sinh từ tools/schema.py.','']
    for i,t in enumerate(TABLES,1):
        doc.add_heading(f'{i} {t["name"]}',2)
        purpose=f'{t["group"]} • '+('Điều chỉnh bảng gốc. ' if t['original'] else 'Bảng bổ sung. ')+t['purpose']
        p=doc.add_paragraph(purpose); p.paragraph_format.keep_with_next=True
        rows=[]; md.extend([f'## {i}. {t["name"]}',purpose,'','| Trường và kiểu | Quy tắc | Ý nghĩa, nguồn và ví dụ |','|---|---|---|'])
        for col in t['columns']:
            explain=col['why']+'\nNguồn: '+source(t,col)+'\nVí dụ: '+col['example']
            rows.append([col['name']+'\n'+col['type'],field_rules(t,col),explain])
            vals=[rows[-1][0],rows[-1][1],explain]
            md.append('| '+' | '.join(v.replace('|','\\|').replace('\n','<br>') for v in vals)+' |')
        doc_table(doc,[t['name']+'\nTrường và kiểu','Quy tắc','Ý nghĩa nguồn và ví dụ'],rows,[1.55,1.55,3.85])
        constraints=[]
        for u in t['uniques']: constraints.append('UNIQUE ('+', '.join(u)+')')
        constraints.extend('CHECK '+s for s in t['checks'])
        for src,cols,dest,keys in DATA['extra_foreign_keys']:
            if src==t['name']: constraints.append(f'FK ghép ({cols}) → {dest}({keys}).')
        for tab,cols,unique,where in DATA['indexes']:
            if tab==t['name']: constraints.append(f'Index {unique} ({cols})'+(' WHERE '+where if where else ''))
        if constraints:
            doc.add_paragraph('Ràng buộc và index bổ sung',style='Heading 3')
            for text in constraints:
                p=doc.add_paragraph(text); p.paragraph_format.space_after=Pt(3)
                for r in p.runs: r.font.size=Pt(9)
                md.append('- '+text)
        md.append('')
    (DOCS/'data-dictionary.md').write_text('\n'.join(md),encoding='utf-8')
    doc.add_heading('Kiểm thử và điều kiện sử dụng',1)
    testpath=ROOT/'qa/test-results.json'
    if testpath.exists():
        tests=json.loads(testpath.read_text(encoding='utf-8'))
        doc.add_paragraph(f'Đã thực thi {tests["passed"]} kiểm tra đạt trên database riêng {tests["database"]}. Các kiểm tra dùng SQL Server thực, có hai kết nối chạy đồng thời cho voucher, điểm và địa chỉ mặc định. Kết quả chi tiết nằm trong verification.md.')
    doc.add_paragraph('Các bài kiểm tra bao gồm đặt pickup/delivery, COD/trả trước, khách vãng lai, retry thanh toán, tổng tiền và điểm, quyền theo chi nhánh/khách, lịch sử giá/địa chỉ, tranh chấp voucher/điểm, hủy/hoàn, khảo sát và doanh thu. Đây là kiểm thử tầng database; chưa phải kiểm thử UI, HTTP API, cổng thanh toán thật hoặc tải thực tế của chuỗi quán.')
    doc.add_paragraph('Dữ liệu mẫu và mật khẩu demo chỉ phục vụ đồ án. Trước khi dùng cho vận hành cần triển khai xác thực backend, cấu hình phân quyền kết nối, lịch xử lý đơn hết hạn, lưu trữ backup và tích hợp thanh toán thực tế theo hướng dẫn README.')
    doc.core_properties.title='Thiết kế cơ sở dữ liệu Morning Brew CRM'
    doc.core_properties.subject='41 bảng cho Admin POS và app khách hàng'
    doc.core_properties.author='Morning Brew CRM'
    dest=DOCS/'MorningBrewCRM_Thiet_ke_database_cap_nhat.docx'
    doc.save(dest)
    print(str(dest))

if __name__=='__main__': make()
