import { Component } from '@angular/core';
import { Troop } from 'src/app/models/troop';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { MatTableDataSource } from '@angular/material/table';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent {
  constructor() {
  }

  async ngOnInit(): Promise<void> {
  }
}
