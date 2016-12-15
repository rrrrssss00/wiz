%IDW���������Ȩ��ֵ����
%����x,y,zΪ��֪���꼰�亯��ֵ,X,YΪҪ��ֵ������
%x,y,z,X,Y���Ϊ��ά�ģ�����Ϊ��ά
%������x��y�г����ظ���������
function [Z]=IDW(x,y,z,X,Y)
[m0,n0]=size(x);
[m1,n1]=size(X);
%���ɾ������r(m0*m1*n1,n0)
for i=1:m1
    for j=1:n1
        r(m0*n1*(i-1)+m0*(j-1)+1:m0*n1*(i-1)+m0*(j),:)=sqrt((X(i,j)-x).^2+(Y(i,j)-y).^2);
    end
end
%�����ֵ����
for i=1:m1
    for j=1:n1
        if find(r(m0*n1*(i-1)+m0*(j-1)+1:m0*n1*(i-1)+m0*(j),:)==0)
            [m2,n2]=find(r(m0*n1*(i-1)+m0*(j-1)+1:m0*n1*(i-1)+m0*(j),:)==0);
            Z(i,j)=z(m2,n2);
        else
            numerator=sum(sum(z./r(m0*n1*(i-1)+m0*(j-1)+1:m0*n1*(i-1)+m0*(j),:)));
            denominator=sum(sum(1./r(m0*n1*(i-1)+m0*(j-1)+1:m0*n1*(i-1)+m0*(j),:)));
            Z(i,j)=numerator/denominator;
        end
    end
end