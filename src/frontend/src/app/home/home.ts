import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';

interface FileItem {
  name: string;
  type: string;
  size: string;
}

const ELEMENT_DATA: FileItem[] = [
  { name: 'Relatório de uso', type: 'PDF', size: '1.2 MB' },
  { name: 'Dados de clientes', type: 'CSV', size: '840 KB' },
  { name: 'Apresentação', type: 'PPTX', size: '4.7 MB' },
  { name: 'Notas de versão', type: 'MD', size: '24 KB' },
];

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, MatTableModule, MatCardModule, MatButtonModule],
  templateUrl: './home.html',
  styleUrls: ['./home.scss'],
})
export class Home {
  protected readonly displayedColumns = ['name', 'type', 'size'];
  protected readonly dataSource = ELEMENT_DATA;
}
